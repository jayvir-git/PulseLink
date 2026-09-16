import assert from 'node:assert/strict';
import { test } from 'node:test';
import { spawn } from 'node:child_process';
import { mkdtemp, rm, rmdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { runLiveBrowser } from './live-browser.mjs';

// Runs the built API with real JWT authentication and only a generated temporary
// SQLite database. No application connection string is accepted or reused.
test('Live API and two browser tabs: conflicts, retry, pagination, and process restart', { timeout: 120000 }, async () => {
  const directory = await mkdtemp(join(tmpdir(), 'pulselink-live-smoke-'));
  const database = join(directory, 'reliability.db');
  const apiDirectory = resolve('../backend/PulseLink.Api');
  const dll = join(apiDirectory, 'bin/Release/net8.0/PulseLink.Api.dll');
  const signingKey = randomUUID() + randomUUID();
  let process, origin;

  async function start() {
    process = spawn('dotnet', [dll, '--urls', 'http://127.0.0.1:0'], {
      cwd: apiDirectory, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'],
      env: { ...globalThis.process.env,
        ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development',
        Database__Provider: 'Sqlite', Database__SeedLoadTest: 'false',
        ConnectionStrings__Default: `Data Source=${database}`,
        ConnectionStrings__Sqlite: `Data Source=${database}`, ConnectionStrings__SqlServer: '',
        Jwt__Key: signingKey, Logging__LogLevel__Default: 'Information',
        Logging__LogLevel__Microsoft: 'Warning',
        'Logging__LogLevel__Microsoft.Hosting.Lifetime': 'Information'
      }
    });
    // Explicit lifetime category overrides the broader Microsoft warning level.
    // Dot-separated environment keys are valid for ProcessStartInfo / spawn.
    origin = await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error('API startup timed out; build Release first.')), 30000);
      let output = '';
      process.stdout.on('data', data => {
        output += data.toString();
        const match = output.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
        if (match) { clearTimeout(timeout); resolve(match[1]); }
      });
      process.stderr.on('data', () => {}); // Do not emit config or request payloads.
      process.once('error', () => { clearTimeout(timeout); reject(new Error('Could not launch dotnet.')); });
      process.once('exit', code => { clearTimeout(timeout); reject(new Error(`API exited during startup (${code}).`)); });
    });
  }

  async function stop() {
    if (!process?.pid || process.exitCode !== null || process.signalCode !== null) return;
    const exited = new Promise(resolve => process.once('exit', resolve));
    process.kill();
    await exited;
  }

  async function call(path, { token, method = 'GET', body, headers = {}, status = 200 } = {}) {
    const response = await fetch(origin + path, {
      signal: AbortSignal.timeout(10000),
      method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...headers },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    const text = await response.text();
    const knownCause = text.includes('UNIQUE constraint failed: Incidents.IncidentNumber')
      ? ' (duplicate incident number)' : '';
    assert.equal(response.status, status, `${method} ${path}: unexpected HTTP status${knownCause}`);
    return text ? JSON.parse(text) : undefined;
  }
  const login = async email => (await call('/api/auth/login', {
    method: 'POST', body: { email, password: 'Demo123!' }
  })).token;

  try {
    await start();
    await call('/api/incidents', { status: 401 });
    const token = await login('paramedic@pulselink.demo');
    const hospitalToken = await login('hospital@pulselink.demo');
    const hospitals = await call('/api/lookup/hospitals', { token });
    let incident = await call('/api/incidents', { token, method: 'POST', status: 201,
      body: { chiefComplaint: 'Synthetic live reliability check', destinationHospitalId: hospitals[0].id } });
    const path = `/api/incidents/${incident.id}`;
    await call(path, { token: hospitalToken, status: 403 });
    const staleVersion = incident.version;
    incident = await call(`${path}/vitals`, { token, method: 'POST',
      headers: { 'If-Match': `"${incident.version}"` }, body: { heartRate: 80 } });
    await call(`${path}/vitals`, { token, method: 'POST', status: 412,
      headers: { 'If-Match': `"${staleVersion}"` }, body: { heartRate: 90 } });
    for (const toStatus of ['EnRoute', 'OnScene', 'Transporting', 'Arrived']) {
      incident = await call(`${path}/status`, { token, method: 'POST',
        headers: { 'If-Match': `"${incident.version}"` }, body: { toStatus } });
    }
    const interventionVersion = incident.version;
    const key = randomUUID();
    const retry = { token, method: 'POST', headers: {
      'If-Match': `"${interventionVersion}"`, 'Idempotency-Key': key
    }, body: { name: 'Synthetic intervention' } };
    const original = await call(`${path}/interventions`, retry);
    incident = await call(path, { token });
    const beforeHandoff = incident.version;
    incident = await call(`${path}/status`, { token, method: 'POST',
      headers: { 'If-Match': `"${beforeHandoff}"` }, body: { toStatus: 'HandedOff' } });
    await call(`${path}/vitals`, { token, method: 'POST', status: 412,
      headers: { 'If-Match': `"${beforeHandoff}"` }, body: { heartRate: 90 } });
    const auditCount = incident.auditEvents.length;

    for (let n = 1; n <= 55; n++) {
      await call('/api/incidents', { token, method: 'POST', status: 201,
        body: { chiefComplaint: `Synthetic pagination ${n}` } });
    }
    const first = await call('/api/incidents?page=1&pageSize=50', { token });
    const second = await call('/api/incidents?page=2&pageSize=50', { token });
    assert.equal(first.items.length, 50);
    assert.ok(second.items.length > 0);
    assert.equal(first.totalCount, second.totalCount);
    assert.equal(new Set([...first.items, ...second.items].map(i => i.id)).size, first.totalCount);

    await runLiveBrowser({ apiOrigin: origin, call, token, hospitalId: hospitals[0].id });

    await stop();
    await start();
    retry.token = await login('paramedic@pulselink.demo');
    assert.deepEqual(await call(`${path}/interventions`, retry), original);
    const after = await call(path, { token: retry.token });
    assert.equal(after.version, incident.version);
    assert.equal(after.interventions.length, 1);
    assert.equal(after.vitalSigns.length, 1);
    assert.equal(after.auditEvents.length, auditCount);
  } finally {
    await stop();
    for (const suffix of ['', '-shm', '-wal']) await rm(database + suffix, { force: true });
    await rmdir(directory);
  }
});

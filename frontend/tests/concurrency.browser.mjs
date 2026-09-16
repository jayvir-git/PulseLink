import assert from 'node:assert/strict';
import { before, after, test } from 'node:test';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { basename, resolve } from 'node:path';
import { chromium } from 'playwright';

// Serve the production build on an ephemeral loopback port. All API calls are
// synthetic fixtures: this test never starts the API or connects to a database.
let server, browser, origin;
before(async () => {
  const dist = resolve('dist');
  server = createServer(async (request, response) => {
    const path = new URL(request.url, 'http://localhost').pathname;
    const asset = path.startsWith('/assets/');
    const file = asset ? resolve(dist, 'assets', basename(path)) : resolve(dist, 'index.html');
    try {
      response.setHeader('Content-Type', asset ? (file.endsWith('.css') ? 'text/css' : 'text/javascript') : 'text/html');
      response.end(await readFile(file));
    } catch {
      response.writeHead(404).end();
    }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  origin = `http://127.0.0.1:${server.address().port}`;
  browser = await chromium.launch({ headless: true, executablePath: process.env.PULSELINK_BROWSER_PATH || undefined });
});
after(async () => {
  await browser?.close();
  if (server) await new Promise(resolve => server.close(resolve));
});

const id = '11111111-0000-0000-0000-000000000001';
const versionA = 'aaaaaaaa-0000-0000-0000-000000000001';
const versionB = 'bbbbbbbb-0000-0000-0000-000000000002';
const versionC = 'cccccccc-0000-0000-0000-000000000003';

async function listScenario(handler) {
  const context = await browser.newContext();
  await context.addInitScript(() => localStorage.setItem('pulselink.auth', JSON.stringify({
    token: 'synthetic', email: 'test@example.invalid', displayName: 'Test paramedic', role: 'Paramedic'
  })));
  await context.route('**/api/**', handler);
  const page = await context.newPage();
  page.setDefaultTimeout(5000);
  await page.goto(origin);
  return { context, page };
}

function listResult(page, totalCount = 55) {
  return { page, pageSize: 50, totalCount, items: Array.from({ length: Math.max(0, Math.min(50, totalCount - (page - 1) * 50)) }, (_, n) => ({
    ...incident(), id: `list-${(page - 1) * 50 + n + 1}`, incidentNumber: `PCR-${(page - 1) * 50 + n + 1}`
  })) };
}

test('Pagination reaches records beyond 50 and respects first/last boundaries', async () => {
  const requested = [];
  const { context, page } = await listScenario(async route => {
    const url = new URL(route.request().url());
    const number = Number(url.searchParams.get('page'));
    requested.push(number);
    assert.equal(url.searchParams.get('pageSize'), '50');
    await route.fulfill({ json: listResult(number) });
  });
  try {
    await page.getByText('1–50 of 55 incidents', { exact: true }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Previous' }).isDisabled(), true);
    await page.getByRole('button', { name: 'Next', exact: true }).click();
    await page.getByText('51–55 of 55 incidents', { exact: true }).waitFor();
    assert.equal(await page.getByRole('link', { name: 'PCR-55', exact: true }).count(), 1);
    assert.equal(await page.getByRole('button', { name: 'Next', exact: true }).isDisabled(), true);
    await page.getByRole('button', { name: 'Previous' }).click();
    await page.getByText('1–50 of 55 incidents', { exact: true }).waitFor();
    assert.deepEqual(requested, [1, 2, 1]);
  } finally { await context.close(); }
});

test('Late page response cannot replace newer navigation', async () => {
  let release, arrived;
  const gate = new Promise(resolve => { release = resolve; });
  const held = new Promise(resolve => { arrived = resolve; });
  const { context, page } = await listScenario(async route => {
    const number = Number(new URL(route.request().url()).searchParams.get('page'));
    if (number === 2) { arrived(); await gate; }
    await route.fulfill({ json: listResult(number) });
  });
  try {
    await page.getByText('1–50 of 55 incidents', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Next', exact: true }).click();
    await held;
    await page.getByText('Loading…', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Previous' }).click();
    await page.getByText('1–50 of 55 incidents', { exact: true }).waitFor();
    const response = page.waitForResponse(r => new URL(r.url()).searchParams.get('page') === '2');
    release();
    await (await response).finished();
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    assert.equal(await page.getByText('1–50 of 55 incidents', { exact: true }).count(), 1);
    assert.equal(await page.getByRole('link', { name: 'PCR-55', exact: true }).count(), 0);
  } finally { release(); await context.close(); }
});

test('List failure has retry and does not masquerade as an empty list', async () => {
  let reads = 0;
  const { context, page } = await listScenario(async route => {
    reads++;
    await route.fulfill(reads === 1
      ? { status: 503, json: { message: 'Synthetic list failure' } }
      : { json: listResult(1, 0) });
  });
  try {
    await page.getByText('Synthetic list failure', { exact: true }).waitFor();
    assert.equal(await page.getByText('No incidents yet.', { exact: true }).count(), 0);
    await page.getByRole('button', { name: 'Retry list' }).click();
    await page.getByText('No incidents yet.', { exact: true }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Next', exact: true }).isDisabled(), true);
    assert.equal(await page.getByRole('button', { name: 'Previous' }).isDisabled(), true);
  } finally { await context.close(); }
});

function incident(version = versionA, status = 'Arrived') {
  return {
    id, version, status, incidentNumber: 'TEST-CONCURRENCY', chiefComplaint: 'Synthetic conflict test',
    agencyName: 'Test EMS', destinationHospitalName: 'Test hospital', agencyId: id, destinationHospitalId: id,
    createdByUserId: 'test-paramedic', createdAt: '2026-09-15T12:00:00Z', updatedAt: '2026-09-15T12:00:00Z',
    patientAgeRange: 'Adult', patientSex: 'Unknown', notes: '',
    allowedNextStatuses: status === 'HandedOff' ? [] : ['HandedOff'],
    vitalSigns: [], interventions: [], auditEvents: []
  };
}

async function scenario(afterConflictStatus, operation, failureMode = 'conflict') {
  const context = await browser.newContext();
  await context.addInitScript(() => localStorage.setItem('pulselink.auth', JSON.stringify({
    token: 'synthetic-test-token', email: 'test@example.invalid', displayName: 'Test paramedic', role: 'Paramedic'
  })));
  let current = incident();
  const writes = [];
  let reads = 0;
  let release;
  const gate = new Promise(resolve => { release = resolve; });
  await context.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === `/api/incidents/${id}/export`) {
      await route.fulfill({ json: { resourceType: 'Bundle', entry: [] } });
    } else if (request.method() === 'GET') {
      reads++;
      await route.fulfill({ json: path === `/api/incidents/${id}` ? current
        : { ...incident(), id: path.split('/').at(-1), chiefComplaint: 'Other synthetic incident' } });
    } else {
      writes.push({ path, version: request.headers()['if-match'], key: request.headers()['idempotency-key'], body: request.postDataJSON() });
      if (writes.length === 1) {
        await gate;
        if (failureMode === 'busy') {
          await route.fulfill({ status: 503, json: { code: 'incident_busy', message: 'Synthetic contention' } });
        } else {
          current = incident(versionB, afterConflictStatus);
          if (failureMode === 'lost') await route.abort('failed');
          else await route.fulfill({ status: 412, json: { code: 'incident_conflict', message: 'This incident changed since you loaded it.' } });
        }
      } else if (failureMode !== 'conflict') {
        const replay = writes.at(-1).key === writes[0].key;
        current = incident(replay ? versionB : versionC, afterConflictStatus);
        await route.fulfill({ json: { incidentId: id, interventionId: replay ? versionB : versionC, performedAt: '2026-09-15T12:00:00Z' } });
      } else {
        current = incident(versionC, operation === 'status' ? 'HandedOff' : afterConflictStatus);
        await route.fulfill({ json: current });
      }
    }
  });
  const page = await context.newPage();
  page.setDefaultTimeout(5000);
  await page.goto(`${origin}/incidents/${id}`);
  await page.getByRole('heading', { name: 'Synthetic conflict test' }).waitFor();
  return { context, page, writes, release, reads: () => reads };
}

test('Navigating to another incident discards the previous incident retry state', async () => {
  const { context, page, release } = await scenario('Arrived', 'intervention', 'lost');
  try {
    await page.getByRole('button', { name: 'Add intervention', exact: true }).click();
    release();
    await page.getByText('The intervention result is uncertain. Keep this page open and retry the same intervention.', { exact: true }).waitFor();
    await page.evaluate(() => {
      history.pushState({}, '', '/incidents/22222222-0000-0000-0000-000000000002');
      dispatchEvent(new PopStateEvent('popstate'));
    });
    await page.getByRole('heading', { name: 'Other synthetic incident' }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Retry same intervention' }).count(), 0);
    assert.equal(await page.getByRole('button', { name: 'Add intervention', exact: true }).isEnabled(), true);
  } finally { release(); await context.close(); }
});

test('Vitals conflict keeps input, requires reload, then uses the refreshed version', { timeout: 30000 }, async () => {
  const s = await scenario('Arrived', 'vitals');
  try {
    const heartRate = s.page.getByLabel('heartRate', { exact: true });
    await heartRate.fill('123');
    await s.page.getByRole('button', { name: 'Generate FHIR-like JSON' }).click();
    await s.page.locator('pre.export').waitFor();
    assert.equal(s.reads(), 1, 'Export must not silently refresh the form version');
    const submit = s.page.getByRole('button', { name: 'Add vitals', exact: true });
    await submit.click();
    assert.equal(await submit.isDisabled(), true);
    s.release();
    await s.page.getByRole('alert').waitFor();
    assert.equal(await heartRate.inputValue(), '123');
    assert.equal(await submit.isDisabled(), true);
    assert.equal(s.writes.length, 1, 'Conflict must not automatically retry');
    assert.equal(s.writes[0].version, `"${versionA}"`);
    await s.page.getByRole('button', { name: 'Reload latest incident' }).click();
    await s.page.getByRole('status').waitFor();
    assert.equal(await heartRate.inputValue(), '123');
    assert.equal(await submit.isEnabled(), true);
    const saved = s.page.waitForResponse(response => response.url().endsWith('/vitals') && response.status() === 200);
    await submit.click();
    await saved;
    assert.equal(s.writes.length, 2);
    assert.equal(s.writes[1].version, `"${versionB}"`);
    assert.equal(s.writes[1].body.heartRate, 123);
  } finally {
    s.release();
    await s.context.close();
  }
});

for (const failureMode of ['lost', 'busy']) {
  test(`Intervention ${failureMode} response reuses the key and payload; a deliberate second action gets a new key`, { timeout: 30000 }, async () => {
    const s = await scenario('Arrived', 'interventions', failureMode);
    try {
      const name = s.page.getByLabel('name', { exact: true });
      await name.fill('Synthetic intended intervention');
      const submit = s.page.getByRole('button', { name: 'Add intervention', exact: true });
      await submit.click();
      s.release();
      await s.page.getByText('The intervention result is uncertain. Keep this page open and retry the same intervention.').waitFor();
      assert.equal(await name.inputValue(), 'Synthetic intended intervention');
      assert.equal(await name.isDisabled(), true);
      assert.equal(await submit.isDisabled(), true);
      assert.equal(s.writes.length, 1);
      assert.match(s.writes[0].key, /^[0-9a-f-]{36}$/);
      await s.page.getByRole('button', { name: 'Retry same intervention' }).click();
      await s.page.getByText('Intervention recorded. Adding again will create a separate intervention.').waitFor();
      await s.page.locator('fieldset:enabled').first().waitFor();
      assert.equal(s.writes.length, 2);
      assert.deepEqual(s.writes[1], s.writes[0], 'Retry must preserve key, payload and original If-Match');
      const savedAgain = s.page.waitForResponse(response => response.url().endsWith('/interventions') && response.status() === 200);
      await submit.click();
      await savedAgain;
      assert.equal(s.writes.length, 3);
      assert.notEqual(s.writes[2].key, s.writes[0].key);
      assert.equal(s.writes[2].version, `"${versionB}"`);
    } finally {
      s.release();
      await s.context.close();
    }
  });
}

test('Handoff conflict preserves intervention input and prevents further clinical submission', { timeout: 30000 }, async () => {
  const s = await scenario('HandedOff', 'interventions');
  try {
    const name = s.page.getByLabel('name', { exact: true });
    await name.fill('Unsaved synthetic intervention');
    await s.page.getByRole('button', { name: 'Add intervention', exact: true }).click();
    s.release();
    await s.page.getByRole('alert').waitFor();
    assert.equal(await name.inputValue(), 'Unsaved synthetic intervention');
    await s.page.getByRole('button', { name: 'Reload latest incident' }).click();
    await s.page.getByRole('status').waitFor();
    assert.equal(await name.inputValue(), 'Unsaved synthetic intervention');
    assert.equal(await name.isDisabled(), true);
    assert.equal(await s.page.getByRole('button', { name: 'Add intervention', exact: true }).isDisabled(), true);
    assert.equal(s.writes.length, 1);
    assert.equal(s.writes[0].version, `"${versionA}"`);
    await s.page.getByText('This incident has been handed off. Your retained inputs cannot be submitted.').waitFor();
    if (process.env.PULSELINK_SCREENSHOT) await s.page.screenshot({ path: process.env.PULSELINK_SCREENSHOT, fullPage: true });
  } finally {
    s.release();
    await s.context.close();
  }
});

test('Status conflict uses the same reload-and-review flow', { timeout: 30000 }, async () => {
  const s = await scenario('Arrived', 'status');
  try {
    await s.page.getByRole('button', { name: 'Mark HandedOff' }).click();
    s.release();
    await s.page.getByRole('alert').waitFor();
    assert.equal(await s.page.getByRole('button', { name: 'Mark HandedOff' }).isDisabled(), true);
    assert.equal(s.writes[0].version, `"${versionA}"`);
    await s.page.getByRole('button', { name: 'Reload latest incident' }).click();
    await s.page.getByRole('status').waitFor();
    await s.page.getByRole('button', { name: 'Mark HandedOff' }).click();
    await s.page.locator('.status-handedoff').waitFor();
    assert.equal(s.writes.length, 2);
    assert.equal(s.writes[1].version, `"${versionB}"`);
  } finally {
    s.release();
    await s.context.close();
  }
});

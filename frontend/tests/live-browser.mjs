import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { basename, resolve } from 'node:path';
import { chromium } from 'playwright';

// Production assets plus a loopback proxy to the isolated real API. No API
// responses are mocked; the only injected fault discards a committed response.
export async function runLiveBrowser({ apiOrigin, call, token, hospitalId }) {
  const dist = resolve('dist');
  const server = createServer(async (request, response) => {
    try {
      const path = new URL(request.url, 'http://localhost').pathname;
      if (path.startsWith('/api/')) {
        const chunks = [];
        for await (const chunk of request) chunks.push(chunk);
        const headers = {};
        for (const name of ['authorization', 'content-type', 'if-match', 'idempotency-key']) {
          if (request.headers[name]) headers[name] = request.headers[name];
        }
        const upstream = await fetch(apiOrigin + request.url, {
          method: request.method, headers, signal: AbortSignal.timeout(10000),
          body: chunks.length ? Buffer.concat(chunks) : undefined
        });
        response.writeHead(upstream.status, { 'Content-Type': upstream.headers.get('content-type') || 'application/json' });
        response.end(Buffer.from(await upstream.arrayBuffer()));
        return;
      }
      const asset = path.startsWith('/assets/');
      const file = asset ? resolve(dist, 'assets', basename(path)) : resolve(dist, 'index.html');
      response.setHeader('Content-Type', asset ? (file.endsWith('.css') ? 'text/css' : 'text/javascript') : 'text/html');
      response.end(await readFile(file));
    } catch { response.writeHead(502).end(); }
  });
  let browser;
  try {
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const origin = `http://127.0.0.1:${server.address().port}`;
    let incident = await call('/api/incidents', { token, method: 'POST', status: 201,
      body: { chiefComplaint: 'Synthetic two-tab walkthrough', destinationHospitalId: hospitalId } });
    const path = `/api/incidents/${incident.id}`;
    for (const toStatus of ['EnRoute', 'OnScene', 'Transporting', 'Arrived']) {
      incident = await call(`${path}/status`, { token, method: 'POST',
        headers: { 'If-Match': `"${incident.version}"` }, body: { toStatus } });
    }
    browser = await chromium.launch({ headless: true, executablePath: process.env.PULSELINK_BROWSER_PATH || undefined });
    const context = await browser.newContext();
    const a = await context.newPage();
    const b = await context.newPage();
    a.setDefaultTimeout(10000);
    b.setDefaultTimeout(10000);
    await a.goto(`${origin}/login`);
    await a.getByLabel('Email', { exact: true }).fill('paramedic@pulselink.demo');
    await a.getByLabel('Password', { exact: true }).fill('Demo123!');
    await a.getByRole('button', { name: 'Sign in', exact: true }).click();
    await a.getByRole('heading', { name: 'Incidents', exact: true }).waitFor();
    for (const page of [a, b]) {
      await page.goto(`${origin}/incidents/${incident.id}`);
      await page.getByRole('heading', { name: 'Synthetic two-tab walkthrough' }).waitFor();
    }
    await b.getByLabel('Heart rate (bpm)', { exact: true }).fill('99');
    await a.getByLabel('Heart rate (bpm)', { exact: true }).fill('80');
    const saved = a.waitForResponse(r => r.url().endsWith('/vitals') && r.request().method() === 'POST');
    await a.getByRole('button', { name: 'Add vitals', exact: true }).click();
    assert.equal((await saved).status(), 200);
    await b.getByRole('button', { name: 'Add vitals', exact: true }).click();
    await b.getByRole('button', { name: 'Reload latest incident' }).waitFor();
    assert.equal(await b.getByLabel('Heart rate (bpm)', { exact: true }).inputValue(), '99');
    await b.getByRole('button', { name: 'Reload latest incident' }).click();
    await b.getByRole('status').waitFor();

    let loseResponse = true;
    const requests = [];
    await a.route(`**${path}/interventions`, async route => {
      const request = route.request();
      requests.push({ key: request.headers()['idempotency-key'], version: request.headers()['if-match'], body: request.postData() });
      if (loseResponse) {
        loseResponse = false;
        const committed = await route.fetch();
        assert.equal(committed.status(), 200);
        await committed.dispose();
        await route.abort('failed');
      } else await route.continue();
    });
    await a.getByLabel('Intervention name', { exact: true }).fill('Synthetic observation');
    await a.getByRole('button', { name: 'Add intervention', exact: true }).click();
    await a.getByText('The intervention result is uncertain. Keep this page open and retry the same intervention.', { exact: true }).waitFor();
    await a.getByRole('button', { name: 'Retry same intervention' }).click();
    await a.getByText('Intervention recorded. Adding again will create a separate intervention.', { exact: true }).waitFor();
    assert.equal(requests.length, 2);
    assert.deepEqual(requests[1], requests[0]);
    await a.getByRole('button', { name: 'Mark HandedOff' }).click();
    await a.locator('.status-handedoff').waitFor();
    await b.getByRole('button', { name: 'Add vitals', exact: true }).click();
    await b.getByRole('button', { name: 'Reload latest incident' }).click();
    await b.locator('.status-handedoff').waitFor();
    assert.equal(await b.getByLabel('Heart rate (bpm)', { exact: true }).inputValue(), '99');
    assert.equal(await b.getByRole('button', { name: 'Add vitals', exact: true }).isDisabled(), true);
    const persisted = await call(path, { token });
    assert.equal(persisted.vitalSigns.length, 1);
    assert.equal(persisted.interventions.length, 1);
    assert.equal(persisted.auditEvents.filter(a => a.action === 'InterventionAdded').length, 1);
    assert.equal(persisted.auditEvents.filter(a => a.action === 'VitalAdded').length, 1);

    const list = await call('/api/incidents?page=1&pageSize=50', { token });
    await a.getByRole('link', { name: '← Back to list' }).click();
    await a.getByText(`1–50 of ${list.totalCount} incidents`, { exact: true }).waitFor();
    await a.getByRole('button', { name: 'Next', exact: true }).click();
    await a.getByText(`51–${list.totalCount} of ${list.totalCount} incidents`, { exact: true }).waitFor();
    assert.equal(await a.getByRole('button', { name: 'Next', exact: true }).isDisabled(), true);
    await a.getByRole('button', { name: 'Previous' }).click();
    await a.getByText(`1–50 of ${list.totalCount} incidents`, { exact: true }).waitFor();
  } finally {
    await browser?.close();
    server.closeAllConnections();
    await new Promise(resolve => server.close(resolve));
  }
}

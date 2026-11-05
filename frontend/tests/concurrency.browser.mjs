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
    await page.getByLabel('Intervention name', { exact: true }).fill('Synthetic observation');
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
    const heartRate = s.page.getByLabel('Heart rate (bpm)', { exact: true });
    await heartRate.fill('123');
    await s.page.getByText('Handoff export', { exact: true }).click();
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
      const name = s.page.getByLabel('Intervention name', { exact: true });
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
      await s.page.getByText('Intervention recorded. The form is ready for the next treatment.').waitFor();
      await s.page.locator('fieldset:enabled').first().waitFor();
      assert.equal(s.writes.length, 2);
      assert.deepEqual(s.writes[1], s.writes[0], 'Retry must preserve key, payload and original If-Match');
      assert.equal(await name.inputValue(), '', 'Confirmed success clears the form for a new treatment');
      await name.fill('Synthetic intended intervention');
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
    const name = s.page.getByLabel('Intervention name', { exact: true });
    await name.fill('Unsaved synthetic intervention');
    await s.page.getByRole('button', { name: 'Add intervention', exact: true }).click();
    s.release();
    await s.page.getByRole('alert').waitFor();
    assert.equal(await name.inputValue(), 'Unsaved synthetic intervention');
    await s.page.getByRole('button', { name: 'Reload latest incident' }).click();
    await s.page.getByRole('status').filter({ hasText: 'Latest incident loaded.' }).waitFor();
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
    await s.page.getByRole('button', { name: 'Mark Handed Off' }).click();
    s.release();
    await s.page.getByRole('alert').waitFor();
    assert.equal(await s.page.getByRole('button', { name: 'Mark Handed Off' }).isDisabled(), true);
    assert.equal(s.writes[0].version, `"${versionA}"`);
    await s.page.getByRole('button', { name: 'Reload latest incident' }).click();
    await s.page.getByRole('status').waitFor();
    await s.page.getByRole('button', { name: 'Mark Handed Off' }).click();
    await s.page.locator('.status-handedoff').waitFor();
    assert.equal(s.writes.length, 2);
    assert.equal(s.writes[1].version, `"${versionB}"`);
  } finally {
    s.release();
    await s.context.close();
  }
});

test('Clinical forms start blank and an empty observation is never submitted', async () => {
  const s = await scenario('Arrived', 'vitals');
  try {
    for (const label of ['Heart rate (bpm)', 'Systolic BP (mmHg)', 'Diastolic BP (mmHg)', 'Respiratory rate (/min)', 'Oxygen saturation (%)', 'Temperature (°C)', 'Glasgow Coma Scale', 'Intervention name', 'Medication', 'Dose', 'Route', 'Intervention notes']) {
      assert.equal(await s.page.getByLabel(label, { exact: true }).inputValue(), '');
    }
    await s.page.getByRole('button', { name: 'Add vitals', exact: true }).click();
    await s.page.getByText('Enter at least one observation before adding vitals.').waitFor();
    assert.equal(s.writes.length, 0);
    await s.page.getByLabel('Heart rate (bpm)', { exact: true }).fill('0');
    await s.page.getByRole('button', { name: 'Add vitals', exact: true }).click();
    s.release();
    await s.page.getByRole('button', { name: 'Reload latest incident' }).waitFor();
    assert.equal(s.writes[0].body.heartRate, 0, 'A recorded zero must not turn into missing data');
    assert.equal(s.writes[0].body.temperatureC, null);
    assert.equal(s.writes[0].body.systolicBp, null);
  } finally { s.release(); await s.context.close(); }
});

test('Public landing leads to sign in and fits a narrow screen', async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  try {
    await page.goto(origin);
    await page.getByRole('heading', { name: /One handoff.*Every detail connected/ }).waitFor();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
    await page.getByRole('link', { name: 'Open your workspace' }).click();
    assert.equal(await page.getByLabel('Email', { exact: true }).inputValue(), '');
    assert.equal(await page.getByLabel('Password', { exact: true }).inputValue(), '');
  } finally { await context.close(); }
});

test('Destination update uses reviewed version, preserves choice on conflict, and unlocks transport', async () => {
  let current = { ...incident(versionA, 'OnScene'), destinationHospitalId: null, destinationHospitalName: null, allowedNextStatuses: ['Transporting'] };
  const writes = [];
  const { context, page } = await listScenario(async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path.endsWith('/hospitals')) return route.fulfill({ json: [{ id, name: 'Test hospital', city: 'Test city' }] });
    if (request.method() === 'PUT') {
      writes.push({ version: request.headers()['if-match'], body: request.postDataJSON() });
      if (writes.length === 1) {
        current = { ...current, version: versionB, notes: 'Newer server note' };
        return route.fulfill({ status: 412, json: { code: 'incident_conflict', message: 'Changed elsewhere' } });
      }
      current = { ...current, ...request.postDataJSON(), destinationHospitalName: 'Test hospital', version: versionC };
      return route.fulfill({ json: current });
    }
    return route.fulfill({ json: path === `/api/incidents/${id}` ? current : { page: 1, pageSize: 50, totalCount: 1, items: [current] } });
  });
  try {
    await page.goto(`${origin}/incidents/${id}`);
    assert.equal(await page.getByRole('button', { name: 'Mark Transporting' }).isDisabled(), true);
    await page.getByRole('button', { name: 'Choose destination' }).click();
    await page.getByRole('combobox').selectOption(id);
    await page.getByRole('button', { name: 'Save destination' }).click();
    await page.getByRole('button', { name: 'Reload latest incident' }).waitFor();
    assert.equal(await page.getByRole('combobox').inputValue(), id);
    await page.getByRole('button', { name: 'Reload latest incident' }).click();
    await page.getByText('Newer server note', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Save destination' }).click();
    await page.getByText('Destination saved.', { exact: false }).waitFor();
    assert.equal(writes[0].version, `"${versionA}"`);
    assert.equal(writes[1].version, `"${versionB}"`);
    assert.equal(writes[1].body.notes, 'Newer server note');
    assert.equal(await page.getByRole('button', { name: 'Mark Transporting' }).isEnabled(), true);
  } finally { await context.close(); }
});

test('Initial detail failure offers announced error and retry', async () => {
  let reads = 0;
  const { context, page } = await listScenario(async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === `/api/incidents/${id}`) {
      reads++;
      return route.fulfill(reads === 1 ? { status: 503, json: { message: 'Temporary incident failure' } } : { json: incident() });
    }
    return route.fulfill({ json: { page: 1, pageSize: 50, totalCount: 0, items: [] } });
  });
  try {
    await page.goto(`${origin}/incidents/${id}`);
    await page.getByRole('alert').filter({ hasText: 'Temporary incident failure' }).waitFor();
    assert.equal(await page.getByRole('link', { name: 'Back to list', exact: false }).count(), 1);
    await page.getByRole('button', { name: 'Retry incident' }).click();
    await page.getByRole('heading', { name: 'Synthetic conflict test' }).waitFor();
    assert.equal(reads, 2);
  } finally { await context.close(); }
});

async function hospitalScenario(handler, viewport = { width: 1440, height: 900 }) {
  const context = await browser.newContext({ viewport });
  await context.addInitScript(hospitalId => localStorage.setItem('pulselink.auth', JSON.stringify({
    token: 'synthetic', email: 'hospital@example.invalid', displayName: 'Receiving nurse', role: 'HospitalStaff', hospitalId,
  })), id);
  await context.route('**/api/**', handler);
  const page = await context.newPage();
  page.setDefaultTimeout(5000);
  await page.goto(`${origin}/incidents/${id}`);
  return { context, page };
}

test('Hospital report keeps latest observation set distinct and exposes treatment notes', async () => {
  const report = { ...incident(versionA, 'Transporting'), notes: 'Synthetic EMS narrative',
    vitalSigns: [
      { id: 'older', recordedAt: '2026-09-15T12:00:00Z', heartRate: 88, systolicBp: 128, diastolicBp: 78 },
      { id: 'newer', recordedAt: '2026-09-15T12:10:00Z', heartRate: 101, temperatureC: 0 },
    ],
    interventions: [{ id: 'treatment', name: 'Synthetic intervention', performedAt: '2026-09-15T12:05:00Z', medication: 'Synthetic medication', dose: 'Test dose', route: 'Test route', notes: 'Recorded response from EMS' }],
  };
  const { context, page } = await hospitalScenario(route => route.fulfill({ json: report }), { width: 390, height: 844 });
  try {
    await page.getByRole('heading', { name: 'Latest observations' }).waitFor();
    const metrics = page.locator('.observation-grid');
    assert.match(await metrics.innerText(), /101/);
    assert.doesNotMatch(await metrics.innerText(), /128|78|88/);
    assert.match(await metrics.innerText(), /Blood pressure\s+Not recorded/);
    assert.match(await metrics.innerText(), /Temperature\s+0/);
    await page.getByText('All observation sets (2)', { exact: true }).click();
    assert.match(await page.getByRole('region', { name: /Observation history/ }).innerText(), /128 \/ 78/);
    assert.equal(await page.getByText('Recorded response from EMS', { exact: false }).count(), 1);
    assert.equal(await page.getByRole('button', { name: 'Acknowledge receipt' }).count(), 0);
    assert.equal(await page.getByRole('button', { name: 'Add vitals' }).count(), 0);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  } finally { await context.close(); }
});

test('Hospital receipt retry shows original receiver and survives refresh', async () => {
  const report = incident(versionA, 'HandedOff');
  const receipt = { id: 'receipt', actorUserId: 'nurse', action: 'HandoffAcknowledged', details: 'Report received by Receiving nurse', createdAt: '2026-09-15T12:30:00Z' };
  let writes = 0;
  const { context, page } = await hospitalScenario(route => {
    if (route.request().method() === 'PUT') {
      writes++;
      report.auditEvents = [receipt]; // First response is lost after commit.
      return writes === 1 ? route.abort('failed') : route.fulfill({ json: receipt });
    }
    return route.fulfill({ json: report });
  });
  try {
    await page.getByRole('button', { name: 'Acknowledge receipt', exact: true }).click();
    await page.getByRole('alert').waitFor();
    await page.getByRole('button', { name: 'Acknowledge receipt', exact: true }).click();
    await page.getByText('Receipt acknowledged', { exact: true }).waitFor();
    assert.equal(writes, 2);
    assert.equal(await page.getByRole('button', { name: 'Acknowledge receipt' }).count(), 0);
    await page.getByRole('button', { name: 'Refresh report' }).click();
    await page.getByText('Report refreshed.', { exact: true }).waitFor();
    assert.match(await page.locator('#report-receipt').innerText(), /Report received by Receiving nurse/);
    assert.equal(await page.locator('#report-receipt time').getAttribute('datetime'), receipt.createdAt);
  } finally { await context.close(); }
});

test('Hospital refresh failure explicitly identifies the retained snapshot', async () => {
  let reads = 0;
  const { context, page } = await hospitalScenario(route => {
    if (new URL(route.request().url()).pathname.endsWith('/export')) return route.fulfill({ json: { resourceType: 'Bundle', entry: [] } });
    reads++;
    return route.fulfill(reads === 1 ? { json: incident() } : { status: 503, json: { message: 'Synthetic connection failure' } });
  });
  try {
    await page.getByRole('heading', { name: 'Synthetic conflict test' }).waitFor();
    await page.getByRole('button', { name: 'Refresh report' }).click();
    await page.getByRole('alert').filter({ hasText: 'Showing the previously loaded report' }).waitFor();
    assert.equal(await page.getByText('Report refreshed.', { exact: true }).count(), 0);
    assert.equal(await page.getByRole('heading', { name: 'Synthetic conflict test' }).count(), 1);
    await page.getByText('Structured export', { exact: true }).click();
    await page.getByRole('button', { name: 'Generate FHIR-like JSON' }).click();
    await page.getByLabel('Exported report JSON').waitFor();
    assert.equal(await page.getByRole('alert').filter({ hasText: 'Showing the previously loaded report' }).count(), 1);
  } finally { await context.close(); }
});


test('Unsaved clinical entries prevent handoff until deliberately cleared', async () => {
  const s = await scenario('Arrived', 'status');
  try {
    for (const [label, value, clear] of [
      ['Heart rate (bpm)', '88', 'Clear observation inputs'],
      ['Intervention notes', 'Response pending', 'Clear treatment inputs'],
      ['EMS narrative', 'New assessment', 'Discard note changes'],
    ]) {
      await s.page.getByLabel(label, { exact: true }).fill(value);
      await s.page.getByRole('button', { name: 'Mark Handed Off' }).click();
      await s.page.getByRole('alert').filter({ hasText: 'Save or clear your unsaved entries' }).waitFor();
      assert.equal(s.writes.length, 0);
      assert.equal(await s.page.getByLabel(label, { exact: true }).inputValue(), value);
      await s.page.getByRole('button', { name: clear, exact: true }).click();
    }
  } finally { s.release(); await s.context.close(); }
});

test('Care notes preserve a conflicting draft, show saved comparison, and retry with reviewed version', async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  await context.addInitScript(() => localStorage.setItem('pulselink.auth', JSON.stringify({ token: 'synthetic', role: 'Paramedic', displayName: 'Test paramedic', email: 'test@example.invalid' })));
  let reads = 0;
  const writes = [];
  await context.route('**/api/**', async route => {
    const req = route.request();
    if (req.method() === 'GET') {
      reads++;
      return route.fulfill({ json: { ...incident(), version: reads === 1 ? versionA : versionB, notes: reads === 1 ? 'Original narrative' : 'Other clinician update' } });
    }
    writes.push({ body: req.postDataJSON(), version: req.headers()['if-match'] });
    if (writes.length === 1) return route.fulfill({ status: 412, json: { message: 'Changed elsewhere' } });
    return route.fulfill({ json: { ...incident(), version: versionC, notes: req.postDataJSON().notes } });
  });
  const page = await context.newPage();
  try {
    await page.goto(`${origin}/incidents/${id}`);
    const notes = page.getByLabel('EMS narrative', { exact: true });
    await notes.fill('Assessment and response documented');
    await page.getByRole('button', { name: 'Save care notes', exact: true }).click();
    await page.getByRole('button', { name: 'Reload latest incident' }).click();
    await page.getByRole('status').filter({ hasText: 'Latest incident loaded' }).waitFor();
    assert.equal(await notes.inputValue(), 'Assessment and response documented');
    await page.getByText('Compare with saved care notes', { exact: true }).click();
    await page.getByText('Other clinician update', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Save care notes', exact: true }).click();
    await page.getByRole('status').filter({ hasText: 'Care notes saved' }).waitFor();
    assert.equal(writes.length, 2);
    assert.equal(writes[1].version, `"${versionB}"`);
    assert.equal(writes[1].body.notes, 'Assessment and response documented');
    assert.equal(await page.getByRole('button', { name: 'Save care notes', exact: true }).isDisabled(), true);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  } finally { await context.close(); }
});

test('Successful intervention replay after handoff must keep other unsaved clinical drafts visible', async () => {
  let current = incident();
  const writes = [];
  const { context, page } = await listScenario(async route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    if (req.method() === 'POST' && path.endsWith('/interventions')) {
      writes.push({ key: req.headers()['idempotency-key'], body: req.postDataJSON() });
      if (writes.length === 1) {
        // Treatment commits; another paramedic subsequently hands off the record.
        // Both are permitted before the client's explicit idempotent replay.
        current = { ...incident(versionC, 'HandedOff'), interventions: [{ id: 'treatment', name: req.postDataJSON().name, performedAt: '2026-09-15T12:01:00Z' }] };
        return route.abort('failed');
      }
      return route.fulfill({ json: { incidentId: id, interventionId: 'treatment', performedAt: '2026-09-15T12:01:00Z' } });
    }
    return route.fulfill({ json: path === `/api/incidents/${id}` ? current : { page: 1, pageSize: 50, totalCount: 1, items: [current] } });
  });
  try {
    await page.goto(`${origin}/incidents/${id}`);
    await page.getByLabel('Heart rate (bpm)', { exact: true }).fill('91');
    await page.getByLabel('EMS narrative', { exact: true }).fill('Unsaved assessment for verbal handoff');
    await page.getByLabel('Intervention name', { exact: true }).fill('Synthetic treatment');
    await page.getByRole('button', { name: 'Add intervention', exact: true }).click();
    await page.getByRole('button', { name: 'Retry same intervention' }).waitFor();
    assert.equal(await page.getByLabel('Heart rate (bpm)', { exact: true }).inputValue(), '91');
    await page.getByRole('button', { name: 'Retry same intervention' }).click();
    await page.getByRole('button', { name: 'Refresh report', exact: true }).waitFor();
    const evidence = {
      retryRequests: writes.length,
      sameKey: writes[0].key === writes[1].key,
      savedTreatmentVisible: await page.getByText('Synthetic treatment', { exact: true }).count(),
      unsavedStateBanner: await page.locator('.field-save-state').innerText(),
      heartRateInputs: await page.getByLabel('Heart rate (bpm)', { exact: true }).count(),
      narrativeInputs: await page.getByLabel('EMS narrative', { exact: true }).count(),
      unsavedNarrativeVisible: await page.getByText('Unsaved assessment for verbal handoff', { exact: true }).count(),
      retainedInputsWarning: await page.getByText('This incident has been handed off. Your retained inputs cannot be submitted.', { exact: true }).count(),
    };
    assert.equal(evidence.sameKey, true);
    assert.equal(evidence.savedTreatmentVisible, 1);
    assert.equal(evidence.retainedInputsWarning, 1);
    assert.equal(await page.getByLabel('Heart rate (bpm)', { exact: true }).inputValue(), '91');
    assert.equal(await page.getByLabel('EMS narrative', { exact: true }).inputValue(), 'Unsaved assessment for verbal handoff');
    assert.equal(await page.getByLabel('Heart rate (bpm)', { exact: true }).isDisabled(), true);
    assert.equal(await page.getByRole('button', { name: 'Save care notes', exact: true }).isDisabled(), true);
    assert.equal(evidence.heartRateInputs, 1, 'Retained vitals must remain visible after replay refresh observes handoff');
    assert.equal(evidence.narrativeInputs, 1, 'Retained narrative must remain visible after handoff');
  } finally { await context.close(); }
});


test('Narrative limit preserves over-limit text and allows saving after shortening', async () => {
  let writes = 0;
  const { context, page } = await listScenario(async route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    if (path === '/api/lookup/hospitals') return route.fulfill({ json: [] });
    if (req.method() === 'PUT') {
      writes++;
      return route.fulfill({ json: { ...incident(versionB), notes: req.postDataJSON().notes } });
    }
    return route.fulfill({ json: path === `/api/incidents/${id}` ? incident() : { page: 1, pageSize: 50, totalCount: 1, items: [incident()] } });
  });
  try {
    await page.goto(`${origin}/incidents/${id}`);
    const notes = page.getByLabel('EMS narrative', { exact: true });
    await notes.fill('n'.repeat(2001));
    assert.equal((await notes.inputValue()).length, 2001);
    assert.equal(await notes.getAttribute('aria-invalid'), 'true');
    assert.equal(await page.getByRole('button', { name: 'Save care notes', exact: true }).isDisabled(), true);
    assert.equal(writes, 0);
    await notes.fill('n'.repeat(2000));
    await page.getByRole('button', { name: 'Save care notes', exact: true }).click();
    await page.getByText('Care notes saved. The receiving team sees this narrative with the report.', { exact: true }).waitFor();
    assert.equal(writes, 1);
    assert.equal((await notes.inputValue()).length, 2000);
    await page.goto(`${origin}/incidents/new`);
    const initial = page.getByLabel('Initial care notes (optional)', { exact: true });
    await initial.fill('n'.repeat(2001));
    assert.equal((await initial.inputValue()).length, 2001);
    assert.equal(await page.getByRole('button', { name: 'Create draft', exact: true }).isDisabled(), true);
    await initial.fill('n'.repeat(2000));
    assert.equal(await page.getByRole('button', { name: 'Create draft', exact: true }).isDisabled(), false);
  } finally { await context.close(); }
});

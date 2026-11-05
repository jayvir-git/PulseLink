import assert from 'node:assert/strict';
import { before, after, test } from 'node:test';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { basename, resolve } from 'node:path';
import { chromium } from 'playwright';

// Serve the demo build on an ephemeral loopback port. No live API calls:
// role switching exercises the real demo screens and browser-local adapter.
let server, browser, origin;
before(async () => {
  const dist = resolve('dist-demo');
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


for (const [role, path] of [['Paramedic', '/'], ['Hospital', '/'], ['Admin', '/'], ['Paramedic', '/incidents/demo-incident-1'], ['Admin', '/admin']]) {
  test(`Switch role opens chooser from ${role} ${path}`, async () => {
    const context = await browser.newContext();
    const page = await context.newPage();
    page.setDefaultTimeout(3000);
    try {
      await page.goto(`${origin}/login`);
      await page.getByRole('button', { name: `Continue as ${role}`, exact: true }).click();
      await page.getByRole('button', { name: 'Switch role', exact: true }).waitFor();
      if (path !== '/') await page.goto(`${origin}${path}`);
      await page.getByRole('button', { name: 'Switch role', exact: true }).click();
      await page.getByRole('heading', { name: 'Choose your perspective.' }).waitFor();
      assert.equal(new URL(page.url()).pathname, '/login');
      assert.equal(await page.getByRole('button', { name: /^Continue as / }).count(), 3);
      await page.getByRole('button', { name: 'Continue as Hospital', exact: true }).click();
      await page.getByRole('heading', { name: 'Incoming handoffs' }).waitFor();
    } finally { await context.close(); }
  });
}


test('Full field-to-hospital flow preserves care and closes with hospital receipt', { timeout: 45000 }, async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(5000);
  async function role(name) {
    await page.getByRole('button', { name: 'Switch role', exact: true }).click();
    await page.getByRole('button', { name: `Continue as ${name}`, exact: true }).click();
  }
  try {
    await page.goto(`${origin}/login`);
    await page.getByRole('button', { name: 'Continue as Paramedic', exact: true }).click();
    await page.getByRole('link', { name: 'New incident', exact: true }).click();
    await page.getByLabel('Chief complaint', { exact: true }).fill('Synthetic full workflow');
    await page.getByLabel('Initial care notes (optional)', { exact: true }).fill('Initial synthetic assessment');
    await page.getByRole('button', { name: 'Create draft', exact: true }).click();
    await page.getByRole('heading', { name: 'Synthetic full workflow', exact: true }).waitFor();
    const url = page.url();
    await page.getByRole('button', { name: 'Mark En Route', exact: true }).click();
    await page.getByRole('button', { name: 'Mark On Scene', exact: true }).click();
    assert.equal(await page.getByRole('button', { name: 'Mark Transporting', exact: true }).isDisabled(), true);
    await page.getByLabel('Heart rate (bpm)', { exact: true }).fill('84');
    await page.getByRole('button', { name: 'Add vitals', exact: true }).click();
    await page.locator('#vitals').getByRole('status').filter({ hasText: 'Vitals recorded' }).waitFor();
    await page.getByLabel('Intervention name', { exact: true }).fill('Synthetic positioning');
    await page.getByLabel('Intervention notes', { exact: true }).fill('Comfort improved on reassessment');
    await page.getByRole('button', { name: 'Add intervention', exact: true }).click();
    await page.locator('#interventions').getByRole('status').filter({ hasText: 'Intervention recorded' }).waitFor();
    await page.getByLabel('EMS narrative', { exact: true }).fill('Updated synthetic assessment and response');
    await page.getByRole('button', { name: 'Save care notes', exact: true }).click();
    await page.locator('#care-notes').getByRole('status').filter({ hasText: 'Care notes saved' }).waitFor();
    await page.getByRole('button', { name: 'Choose destination', exact: true }).click();
    await page.getByLabel('Receiving hospital', { exact: true }).selectOption({ label: 'Cooper University Hospital' });
    await page.getByRole('button', { name: 'Save destination', exact: true }).click();
    await page.getByRole('button', { name: 'Mark Transporting', exact: true }).click();
    await role('Hospital');
    const row = page.getByRole('row').filter({ hasText: 'Synthetic full workflow' });
    await row.getByRole('link').click();
    await page.getByText('EMS handoff not complete', { exact: true }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Acknowledge receipt', exact: true }).count(), 0);
    await page.getByText('Comfort improved on reassessment', { exact: false }).waitFor();
    await page.getByText('Updated synthetic assessment and response', { exact: true }).waitFor();
    await role('Paramedic');
    await page.goto(url);
    await page.getByRole('button', { name: 'Mark Arrived', exact: true }).click();
    await page.getByRole('button', { name: 'Mark Handed Off', exact: true }).click();
    await page.getByText('EMS handoff complete · awaiting receipt', { exact: true }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Add vitals', exact: true }).count(), 0);
    await role('Hospital');
    await page.goto(url);
    await page.getByRole('button', { name: 'Acknowledge receipt', exact: true }).click();
    await page.getByText('Receipt acknowledged', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Refresh report', exact: true }).click();
    await page.getByText('Receipt acknowledged', { exact: true }).waitFor();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await role('Paramedic');
    await page.goto(url);
    await page.getByRole('button', { name: 'Refresh report', exact: true }).click();
    await page.getByText('Report received', { exact: true }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Add intervention', exact: true }).count(), 0);
  } finally { await context.close(); }
});


test('Hospital queues distinguish arrival from acknowledgment and update after receipt', async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(5000);
  try {
    await page.goto(`${origin}/login`);
    await page.getByRole('button', { name: 'Continue as Hospital', exact: true }).click();
    await page.getByRole('button', { name: /Awaiting acknowledgment.*Handoff complete/ }).click();
    await page.getByRole('link', { name: 'INC-2026-0005', exact: true }).waitFor();
    assert.equal(await page.getByRole('link', { name: 'INC-2026-0004', exact: true }).count(), 0);
    await page.getByRole('link', { name: 'INC-2026-0005', exact: true }).click();
    await page.getByRole('button', { name: 'Acknowledge receipt', exact: true }).click();
    await page.getByText('Receipt acknowledged', { exact: true }).waitFor();
    await page.getByRole('link', { name: '← Incoming handoffs', exact: true }).click();
    await page.getByRole('button', { name: /Awaiting acknowledgment.*Handoff complete/ }).click();
    await page.getByText('No reports awaiting acknowledgment.', { exact: true }).waitFor();
    await page.getByRole('button', { name: /Acknowledged.*Report receipt confirmed/ }).click();
    await page.getByRole('link', { name: 'INC-2026-0005', exact: true }).waitFor();
    await page.getByRole('button', { name: /Arrived.*EMS handoff pending/ }).click();
    await page.getByRole('link', { name: 'INC-2026-0004', exact: true }).waitFor();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  } finally { await context.close(); }
});


test('Queue hover, selected controls and disabled buttons retain readable contrast; identity wraps deliberately', async () => {
  const context = await browser.newContext();
  const page = await context.newPage();
  page.setDefaultTimeout(5000);
  async function checkContrast(locator) {
    const results = await locator.evaluateAll(elements => {
      const rgb = value => value.match(/[\d.]+/g).map(Number);
      const luminance = color => {
        const c = rgb(color).slice(0, 3).map(v => { v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; });
        return .2126 * c[0] + .7152 * c[1] + .0722 * c[2];
      };
      return elements.map(el => {
        let node = el;
        while (node && (getComputedStyle(node).backgroundColor === 'transparent' || rgb(getComputedStyle(node).backgroundColor)[3] === 0)) node = node.parentElement;
        const bg = node ? getComputedStyle(node).backgroundColor : 'rgb(255,255,255)';
        const fg = getComputedStyle(el).color;
        const a = luminance(fg), b = luminance(bg);
        return { text: el.textContent, ratio: (Math.max(a,b)+.05)/(Math.min(a,b)+.05) };
      });
    });
    for (const item of results) assert.ok(item.ratio >= 4.5, `${item.text}: contrast ${item.ratio}`);
  }
  try {
    await page.goto(`${origin}/login`);
    for (const button of await page.locator('.role-option').all()) {
      await button.hover();
      await checkContrast(button.locator('strong, span'));
    }
    await page.getByRole('button', { name: 'Continue as Hospital', exact: true }).click();
    await page.getByRole('heading', { name: 'Incoming handoffs' }).waitFor();
    for (const button of await page.locator('.queue-filters button').all()) {
      await button.hover();
      await checkContrast(button.locator('span, strong, small'));
      await button.click();
      await page.getByRole('button', { name: 'Refresh list', exact: true }).waitFor();
      await checkContrast(button.locator('span, strong, small'));
    }
    const all = page.getByRole('button', { name: 'All reports', exact: true });
    await all.click(); await all.hover(); await checkContrast(all);
    await page.getByRole('button', { name: 'Refresh list', exact: true }).hover();
    await checkContrast(page.getByRole('button', { name: 'Refresh list', exact: true }));
    await checkContrast(page.getByRole('button', { name: 'Previous', exact: true }));
    await checkContrast(page.getByRole('button', { name: 'Switch role', exact: true }));
    await page.locator('.queue-filters button').first().focus();
    await page.keyboard.press('Tab');
    assert.equal(await page.locator('.queue-filters button').nth(1).evaluate(el => el.matches(':focus-visible') && parseFloat(getComputedStyle(el).outlineWidth) >= 3), true);
    for (const width of [320, 390, 780, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      const layout = await page.locator('.user-chip').evaluate(el => {
        const name = el.querySelector('.user-name').getBoundingClientRect();
        const role = el.querySelector('.user-role').getBoundingClientRect();
        const button = el.querySelector('button').getBoundingClientRect();
        return { roleBelowName: role.top >= name.bottom, noOverlap: role.right <= button.left, nowrap: getComputedStyle(el.querySelector('.user-role')).whiteSpace === 'nowrap', fits: document.documentElement.scrollWidth <= innerWidth };
      });
      assert.deepEqual(layout, { roleBelowName: true, noOverlap: true, nowrap: true, fits: true });
    }
  } finally { await context.close(); }
});

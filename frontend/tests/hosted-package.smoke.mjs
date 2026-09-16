import assert from 'node:assert/strict';
import { test } from 'node:test';
import { spawn } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { chromium } from 'playwright';

test('Published app serves the SPA and authenticates with hosted credentials', { timeout: 90000 }, async () => {
  assert.ok(process.env.PULSELINK_PACKAGE_PATH, 'Set PULSELINK_PACKAGE_PATH to the published API directory.');
  const directory = await mkdtemp(join(tmpdir(), 'pulselink-hosted-'));
  const published = resolve(process.env.PULSELINK_PACKAGE_PATH);
  const password = `Synthetic-${randomUUID()}-1!`;
  let child, browser;
  try {
    child = spawn('dotnet', [join(published, 'PulseLink.Api.dll'), '--urls', 'http://127.0.0.1:0'], {
      cwd: published, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'],
      env: { ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Production', DOTNET_ENVIRONMENT: 'Production',
        Database__Provider: 'Sqlite', Database__MigrateOnStartup: 'true', Database__SeedLoadTest: 'false',
        ConnectionStrings__Sqlite: `Data Source=${join(directory, 'hosted.db')}`,
        Jwt__Key: randomUUID() + randomUUID(), Demo__Password: password,
        'Logging__LogLevel__Microsoft.Hosting.Lifetime': 'Information'
      }
    });
    const origin = await new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('Published API startup timed out.')), 45000);
      let output = '';
      child.stdout.on('data', data => {
        output += data;
        const match = output.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
        if (match) { clearTimeout(timer); resolve(match[1]); }
      });
      child.stderr.on('data', () => {});
      child.once('error', () => { clearTimeout(timer); reject(new Error('Could not launch published API.')); });
      child.once('exit', code => { clearTimeout(timer); reject(new Error(`Published API exited (${code}).`)); });
    });
    assert.equal((await fetch(`${origin}/health`)).status, 200);
    assert.equal((await fetch(`${origin}/api/nonexistent`)).status, 404);
    assert.equal((await fetch(`${origin}/api/incidents`)).status, 401);
    const direct = await fetch(`${origin}/incidents/new`);
    assert.equal(direct.status, 200);
    assert.match(direct.headers.get('content-type'), /text\/html/);
    const defaultLogin = await fetch(`${origin}/api/auth/login`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'paramedic@pulselink.demo', password: 'Demo123!' })
    });
    assert.equal(defaultLogin.status, 401);
    browser = await chromium.launch({ headless: true, executablePath: process.env.PULSELINK_BROWSER_PATH || undefined });
    const page = await browser.newPage();
    await page.goto(origin);
    assert.equal(await page.getByLabel('Email', { exact: true }).inputValue(), '');
    assert.equal(await page.getByLabel('Password', { exact: true }).inputValue(), '');
    assert.equal(await page.getByText('Demo accounts (password: Demo123!)').count(), 0);
    await page.getByLabel('Email', { exact: true }).fill('paramedic@pulselink.demo');
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await page.getByRole('button', { name: 'Sign out', exact: true }).waitFor();
  } finally {
    await browser?.close();
    if (child?.pid && child.exitCode === null && child.signalCode === null) {
      const exited = new Promise(resolve => child.once('exit', resolve));
      child.kill();
      await exited;
    }
    await rm(directory, { recursive: true, force: true });
  }
});

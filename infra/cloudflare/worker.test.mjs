import assert from 'node:assert/strict';
import { test } from 'node:test';
import worker from './worker.mjs';

test('Proxy preserves authenticated writes and does not cache clinical responses', async t => {
  t.mock.method(globalThis, 'fetch', async (request, options) => {
    assert.equal(request.url, 'https://pulselink-jayvir-demo-c3gqchgzgrg2hfcr.centralus-01.azurewebsites.net/api/incidents?destination=other.example');
    assert.equal(request.method, 'POST');
    assert.equal(request.headers.get('Authorization'), 'Bearer synthetic');
    assert.equal(request.headers.get('If-Match'), '"version"');
    assert.equal(request.headers.get('Idempotency-Key'), 'synthetic-key');
    assert.equal(await request.text(), '{"name":"Synthetic"}');
    assert.equal(request.redirect, 'manual');
    assert.equal(options.cf.cacheTtl, 0);
    return new Response('{"saved":true}', { status: 201, headers: { ETag: '"next"' } });
  });
  const response = await worker.fetch(new Request('https://pulselink.jayvir.dev/api/incidents?destination=other.example', {
    method: 'POST', headers: { Authorization: 'Bearer synthetic', 'If-Match': '"version"', 'Idempotency-Key': 'synthetic-key' },
    body: '{"name":"Synthetic"}',
  }));
  assert.equal(response.status, 201);
  assert.equal(response.headers.get('ETag'), '"next"');
  assert.equal(response.headers.get('Cache-Control'), 'no-store');
});

test('Upstream redirects retain the custom hostname', async t => {
  t.mock.method(globalThis, 'fetch', async () => new Response(null, { status: 302, headers: { Location: '/login' } }));
  const response = await worker.fetch(new Request('https://pulselink.jayvir.dev/'));
  assert.equal(response.headers.get('Location'), 'https://pulselink.jayvir.dev/login');
});

test('Upstream network failures produce a generic uncached error', async t => {
  t.mock.method(globalThis, 'fetch', async () => { throw new Error('Internal connection details'); });
  const response = await worker.fetch(new Request('https://pulselink.jayvir.dev/'));
  assert.equal(response.status, 503);
  assert.equal(response.headers.get('Cache-Control'), 'no-store');
  assert.doesNotMatch(await response.text(), /Internal/);
});

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

test('Hashed bundles are held at the edge and by the browser', async t => {
  let seen = null;
  t.mock.method(globalThis, 'fetch', async (request, options) => {
    seen = options.cf;
    return new Response('body{}', { status: 200, headers: { 'Content-Type': 'text/css' } });
  });
  const response = await worker.fetch(new Request('https://pulselink.jayvir.dev/assets/index-CmHB_6VB.css'));
  assert.equal(response.status, 200);
  assert.equal(response.headers.get('Cache-Control'), 'public, max-age=31536000, immutable');
  assert.equal(seen.cacheEverything, true);
  assert.equal(seen.cacheTtlByStatus['200-299'], 31536000);
  assert.equal(seen.cacheTtlByStatus['400-599'], 0);
});

test('Unhashed files copied from public get a short TTL', async t => {
  t.mock.method(globalThis, 'fetch', async () => new Response('<svg/>', { status: 200 }));
  const response = await worker.fetch(new Request('https://pulselink.jayvir.dev/pulselink.svg'));
  assert.equal(response.headers.get('Cache-Control'), 'public, max-age=3600');
});

test('The document and SPA routes are never cached', async t => {
  t.mock.method(globalThis, 'fetch', async (request, options) => {
    assert.equal(options.cf.cacheTtl, 0);
    assert.equal(options.cf.cacheEverything, false);
    return new Response('<!doctype html>', { status: 200, headers: { 'Content-Type': 'text/html' } });
  });
  for (const path of ['/', '/login', '/incidents/synthetic-id']) {
    const response = await worker.fetch(new Request('https://pulselink.jayvir.dev' + path));
    assert.equal(response.headers.get('Cache-Control'), 'no-store', path);
  }
});

test('An asset request carrying credentials is not cached', async t => {
  t.mock.method(globalThis, 'fetch', async (request, options) => {
    assert.equal(options.cf.cacheTtl, 0);
    return new Response('body{}', { status: 200 });
  });
  const response = await worker.fetch(new Request('https://pulselink.jayvir.dev/assets/index-CmHB_6VB.css', {
    headers: { Authorization: 'Bearer synthetic' },
  }));
  assert.equal(response.headers.get('Cache-Control'), 'no-store');
});

test('A missing asset is not pinned at the edge', async t => {
  t.mock.method(globalThis, 'fetch', async () => new Response('Not Found', { status: 404 }));
  const response = await worker.fetch(new Request('https://pulselink.jayvir.dev/assets/index-Gone.js'));
  assert.equal(response.status, 404);
  assert.equal(response.headers.get('Cache-Control'), 'no-store');
});

test('A write to an asset path is not cached', async t => {
  t.mock.method(globalThis, 'fetch', async (request, options) => {
    assert.equal(options.cf.cacheTtl, 0);
    return new Response(null, { status: 405 });
  });
  const response = await worker.fetch(new Request('https://pulselink.jayvir.dev/assets/index-CmHB_6VB.css', { method: 'POST' }));
  assert.equal(response.headers.get('Cache-Control'), 'no-store');
});

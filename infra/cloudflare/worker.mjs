const ORIGIN = 'https://pulselink-jayvir-demo-c3gqchgzgrg2hfcr.centralus-01.azurewebsites.net';

// Vite writes content-hashed filenames into /assets, so one of those URLs never
// changes meaning and can be held for a year. Files copied verbatim from public/
// keep their names across deployments, so they get a short TTL instead.
const HASHED_PREFIX = '/assets/';
const UNHASHED_STATIC = /\.(?:svg|png|ico|webmanifest|woff2?)$/i;
const YEAR = 31536000;
const HOUR = 3600;

// Default deny. A path only becomes cacheable by matching a rule here; the
// document, SPA routes and every /api path fall through and keep reaching the
// origin, because they are what point at the hashed filenames above.
function cachePolicy(request, url) {
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    return null;
  }
  // A request carrying credentials is never a static asset request. Refuse to
  // cache it rather than risk holding a clinical response at the edge.
  if (request.headers.has('Authorization')) {
    return null;
  }
  if (url.pathname.startsWith(HASHED_PREFIX)) {
    return { ttl: YEAR, header: 'public, max-age=' + YEAR + ', immutable' };
  }
  if (UNHASHED_STATIC.test(url.pathname)) {
    return { ttl: HOUR, header: 'public, max-age=' + HOUR };
  }
  return null;
}

export default {
  async fetch(request) {
    const incoming = new URL(request.url);
    const target = new URL(ORIGIN);
    target.pathname = incoming.pathname;
    target.search = incoming.search;
    const headers = new Headers(request.headers);
    headers.delete('host');
    // Keep the upstream fixed; never accept a destination from query parameters.
    const upstream = new Request(new Request(target, request), {
      headers,
      redirect: 'manual',
    });
    const policy = cachePolicy(request, incoming);
    // Only a success is worth storing. Scoping the TTL by status stops a bad
    // deployment from pinning a 404 at the edge for a year.
    const cf = policy
      ? { cacheTtlByStatus: { '200-299': policy.ttl, '300-399': 0, '400-599': 0 }, cacheEverything: true }
      : { cacheTtl: 0, cacheEverything: false };
    try {
      const response = await fetch(upstream, { cf });
      const outgoing = new Headers(response.headers);
      const cacheable = policy && response.status >= 200 && response.status < 300;
      outgoing.set('Cache-Control', cacheable ? policy.header : 'no-store');
      const location = outgoing.get('Location');
      if (location) {
        const redirect = new URL(location, target);
        if (redirect.origin === ORIGIN) {
          redirect.protocol = incoming.protocol;
          redirect.host = incoming.host;
          outgoing.set('Location', redirect.href);
        }
      }
      return new Response(response.body, {
        status: response.status,
        statusText: response.statusText,
        headers: outgoing,
      });
    } catch {
      return new Response('PulseLink is temporarily unavailable. Please try again shortly.', {
        status: 503,
        headers: { 'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-store' },
      });
    }
  },
};

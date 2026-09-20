const ORIGIN = 'https://pulselink-jayvir-demo-c3gqchgzgrg2hfcr.centralus-01.azurewebsites.net';

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
    try {
      const response = await fetch(upstream, { cf: { cacheTtl: 0, cacheEverything: false } });
      const outgoing = new Headers(response.headers);
      outgoing.set('Cache-Control', 'no-store');
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

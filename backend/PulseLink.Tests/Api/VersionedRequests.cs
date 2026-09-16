using System.Net.Http.Json;
using System.Text.Json;

namespace PulseLink.Tests.Api;

internal static class VersionedRequests
{
    public static async Task<string> ReadVersionAsync(this HttpClient client, Guid id)
    {
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/incidents/{id}");
        return detail.GetProperty("version").GetString()!;
    }

    public static Task<HttpResponseMessage> PutIncidentAsync(this HttpClient client, string path, object body) =>
        WithCurrentVersionAsync(client, HttpMethod.Put, path, body);

    public static Task<HttpResponseMessage> PostIncidentAsync(this HttpClient client, string path, object body) =>
        WithCurrentVersionAsync(client, HttpMethod.Post, path, body);

    // Existing behavior tests intentionally use a fresh version. Concurrency tests
    // send an explicit captured version instead, so this cannot mask stale writes.
    private static async Task<HttpResponseMessage> WithCurrentVersionAsync(HttpClient client, HttpMethod method, string path, object body)
    {
        if (path == "/api/incidents") return await client.PostAsJsonAsync(path, body);
        var id = Guid.Parse(path.Split('/')[3]);
        return await client.SendVersionAsync(method, path, body, await client.ReadVersionAsync(id));
    }

    public static async Task<HttpResponseMessage> SendVersionAsync(this HttpClient client, HttpMethod method,
        string path, object body, string version)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        if (path.EndsWith("/interventions", StringComparison.Ordinal))
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}

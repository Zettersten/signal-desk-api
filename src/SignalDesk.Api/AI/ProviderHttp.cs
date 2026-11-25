using System.Net.Http.Headers;
using System.Text;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.AI;

public static class ProviderHttp
{
    public static string Key(IConfiguration configuration, string name) =>
        !string.IsNullOrWhiteSpace(configuration[name]) ? configuration[name]! :
        throw new ApiException(503, "Required AI provider credentials are not configured.");

    public static async Task<T> PostAsync<T>(HttpClient client, string address, string key, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(Wire.Encode(payload), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? 429 : 503,
                "AI provider unavailable or rejected the request.");
        await response.Content.LoadIntoBufferAsync(2_000_000, ct);
        return Wire.Decode<T>(await response.Content.ReadAsStringAsync(ct));
    }
}

using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.AI;

public sealed class PerplexitySearchService(
    HttpClient http, IConfiguration configuration, AiLedger ledger, IOptions<AiOptions> ai) : ISearchService
{
    public Task<SearchResult> SearchAsync(string[] queries, string[] domains, DateTime since, CancellationToken ct)
    {
        if (queries.Length is < 1 or > 5 || queries.Any(q => string.IsNullOrWhiteSpace(q) || System.Text.Encoding.UTF8.GetByteCount(q) > 8192) ||
            domains.Length is < 1 or > 20 || domains.Any(d => !Uri.CheckHostName(d).Equals(UriHostNameType.Dns)))
            throw new ApiException(400, "Invalid bounded search plan.");
        var key = ProviderHttp.Key(configuration, "PERPLEXITY_API_KEY");
        var payload = new
        {
            query = queries,
            max_results = 20,
            search_domain_filter = domains,
            search_after_date_filter = since.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture),
            search_language_filter = new[] { "en", "fr" }
        };
        return ledger.ExecuteAsync("perplexity", "search", "search", payload, ai.Value.SearchQueryReserveUsd * queries.Length,
            async token =>
            {
                var result = await ProviderHttp.PostAsync<SearchResult>(http, "https://api.perplexity.ai/search", key, payload, token);
                if (result.Results is null || result.Results.Length > 20 || string.IsNullOrWhiteSpace(result.Id) ||
                    result.Results.Any(h => string.IsNullOrWhiteSpace(h.Url) || h.Title is null || h.Snippet is null))
                    throw new ApiException(503, "Perplexity returned an invalid search result.");
                return result;
            },
            ct, queries.Length);
    }
}

using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.AI;

public interface ISearchService
{
    Task<SearchResult> SearchAsync(string[] queries, string[] domains, DateTime since, CancellationToken ct);
}

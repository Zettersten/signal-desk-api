using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.AI;

public sealed record SearchResult(string Id, SearchHit[] Results) : IAiResultMetadata
{
    string? IAiResultMetadata.ResolvedModel => "search";
    string? IAiResultMetadata.RequestId => Id;
    long? IAiResultMetadata.InputTokens => null;
    long? IAiResultMetadata.OutputTokens => null;
}

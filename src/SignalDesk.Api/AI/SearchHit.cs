using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.AI;

public sealed record SearchHit(string Title, string Url, string Snippet, string? Date = null,
    [property: JsonPropertyName("last_updated")] string? LastUpdated = null);

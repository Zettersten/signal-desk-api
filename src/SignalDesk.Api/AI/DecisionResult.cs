using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.AI;

public sealed record DecisionResult(string Model, IReadOnlyDictionary<string, JsonElement> Answers, ProviderUsage? Usage = null) : IAiResultMetadata
{
    string? IAiResultMetadata.ResolvedModel => Model;
    string? IAiResultMetadata.RequestId => null;
    long? IAiResultMetadata.InputTokens => Usage?.InputTokens;
    long? IAiResultMetadata.OutputTokens => Usage?.OutputTokens;
}

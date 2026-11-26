using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.AI;

public sealed record ProviderUsage(
    [property: System.Text.Json.Serialization.JsonPropertyName("input_tokens")] long? InputTokens,
    [property: System.Text.Json.Serialization.JsonPropertyName("output_tokens")] long? OutputTokens);

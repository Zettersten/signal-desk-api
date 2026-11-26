using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed record AiExecutionInfo(string Id, string Provider, string Operation, string Model, string Status,
    DateTime CreatedUtc, decimal ReservedUsd, string? RequestId, long? InputTokens, long? OutputTokens,
    string? ResolutionNote);

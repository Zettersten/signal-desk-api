using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.AI;

public interface IAiResultMetadata
{
    string? ResolvedModel { get; }
    string? RequestId { get; }
    long? InputTokens { get; }
    long? OutputTokens { get; }
}

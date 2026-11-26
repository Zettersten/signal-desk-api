using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class ResolveExecutionRequest
{
    public string Id { get; set; } = "";
    public string Note { get; set; } = "";
}

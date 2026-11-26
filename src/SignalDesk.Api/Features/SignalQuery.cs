using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class SignalQuery
{
    public string? CompetitorId { get; set; }
    public string? PublisherId { get; set; }
    public string? Category { get; set; }
    public int MinImportance { get; set; }
    public DateTime? After { get; set; }
    public DateTime? Before { get; set; }
    public string? Query { get; set; }
    public string? Cursor { get; set; }
    public int Limit { get; set; } = 50;
}

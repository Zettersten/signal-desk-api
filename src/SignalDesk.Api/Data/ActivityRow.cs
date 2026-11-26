using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class ActivityRow
{
    public string Id { get; set; } = "";
    public string CompetitorId { get; set; } = "";
    public CompetitorRow Competitor { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
    public string PayloadJson { get; set; } = "";
}

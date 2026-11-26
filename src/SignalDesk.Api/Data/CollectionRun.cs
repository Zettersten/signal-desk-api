using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class CollectionRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceId { get; set; } = "";
    public string State { get; set; } = "queued";
    public Guid? JobTrackingId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public int Discovered { get; set; }
    public int Published { get; set; }
    public int Reviewed { get; set; }
    public string? Error { get; set; }
    public string? Step { get; set; }
    public int Processed { get; set; }
    public int? Total { get; set; }
}

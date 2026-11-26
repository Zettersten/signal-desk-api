using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class ReviewRow
{
    public string Id { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public DocumentRow Document { get; set; } = null!;
    public string Reason { get; set; } = "";
    public string State { get; set; } = "pending";
    public string? ProposalJson { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public string? DecidedBy { get; set; }
    public string? DecisionNote { get; set; }
}

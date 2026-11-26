using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class ProfileFact
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CompetitorId { get; set; } = "";
    public CompetitorRow Competitor { get; set; } = null!;
    public string DocumentId { get; set; } = "";
    public DocumentRow Document { get; set; } = null!;
    public string Kind { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Currency { get; set; }
    public string? Period { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public bool Approved { get; set; }
}

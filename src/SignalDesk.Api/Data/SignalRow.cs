using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class SignalRow
{
    public string Id { get; set; } = "";
    public string CompetitorId { get; set; } = "";
    public CompetitorRow Competitor { get; set; } = null!;
    public string PublisherId { get; set; } = "";
    public PublisherRow Publisher { get; set; } = null!;
    public string? DocumentId { get; set; }
    public DocumentRow? Document { get; set; }
    public DateTime PublishedUtc { get; set; }
    public int Importance { get; set; }
    public string Category { get; set; } = "";
    public string PayloadJson { get; set; } = "";
}

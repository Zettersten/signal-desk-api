using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class DocumentRow
{
    public string Id { get; set; } = "";
    public string SourceId { get; set; } = "";
    public SourceRow Source { get; set; } = null!;
    public string CanonicalUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime? PublishedUtc { get; set; }
    public string DatePrecision { get; set; } = "unknown";
    public DateTime FetchedUtc { get; set; }
    public string ContentHash { get; set; } = "";
    public string ParagraphsJson { get; set; } = "[]";
    public string Availability { get; set; } = "excerpt";
    public string State { get; set; } = "captured";
}

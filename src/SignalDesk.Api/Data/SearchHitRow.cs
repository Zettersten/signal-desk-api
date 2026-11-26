using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class SearchHitRow
{
    public string Id { get; set; } = "";
    public string SourceId { get; set; } = "";
    public SourceRow Source { get; set; } = null!;
    public string Url { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string? Date { get; set; }
    public DateTime DiscoveredUtc { get; set; }
    public DateTime? LastCheckedUtc { get; set; }
    public string State { get; set; } = "discovered";
}

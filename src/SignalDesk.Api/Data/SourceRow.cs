using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class SourceRow
{
    public string Id { get; set; } = "";
    public string PublisherId { get; set; } = "";
    public PublisherRow Publisher { get; set; } = null!;
    public string Url { get; set; } = "";
    public string Kind { get; set; } = "search";
    public bool Approved { get; set; }
    public bool AllowArticleCapture { get; set; }
    public string? RightsAcknowledgement { get; set; }
    public string Status { get; set; } = "pending-approval";
    public DateTime NextDueUtc { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
}

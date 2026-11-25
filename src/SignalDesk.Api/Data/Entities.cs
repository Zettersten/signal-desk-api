using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class DatasetRow
{
    public int Id { get; set; } = 1;
    public string Mode { get; set; } = "";
}

public sealed class CompetitorRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AliasesJson { get; set; } = "[]";
    public string ProfileJson { get; set; } = "";
}

public sealed class PublisherRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Website { get; set; } = "";
    public string TopicsJson { get; set; } = "[]";
    public string ProfileJson { get; set; } = "";
}

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

public sealed class ActivityRow
{
    public string Id { get; set; } = "";
    public string CompetitorId { get; set; } = "";
    public CompetitorRow Competitor { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
    public string PayloadJson { get; set; } = "";
}

public sealed class AiExecution
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Provider { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Model { get; set; } = "";
    public string Version { get; set; } = "v1";
    public string InputHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public string Status { get; set; } = "reserved";
    public decimal ReservedUsd { get; set; }
    public int Units { get; set; } = 1;
    public string? RequestId { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public string? ResultJson { get; set; }
    public string? ResolutionNote { get; set; }
    public string? ResolvedBy { get; set; }
}

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
}

public sealed class JobRecord : IJobStorageRecord
{
    public Guid TrackingID { get; set; }
    public string QueueID { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTime ExecuteAfter { get; set; }
    public DateTime ExpireOn { get; set; }
    public DateTime DequeueAfter { get; set; }
    public bool IsComplete { get; set; }
    public int Attempts { get; set; }
    public string State { get; set; } = "pending";
    public string? Error { get; set; }
    [NotMapped] public object Command { get; set; } = null!;
    TCommand IJobStorageRecord.GetCommand<TCommand>() => Wire.Decode<TCommand>(PayloadJson);
    void IJobStorageRecord.SetCommand<TCommand>(TCommand command) => PayloadJson = Wire.Encode(command);
}

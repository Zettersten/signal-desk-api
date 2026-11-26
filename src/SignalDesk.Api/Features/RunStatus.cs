namespace SignalDesk.Api.Features;

public sealed record RunStatus(
    Guid Id,
    string SourceId,
    string State,
    string? Step,
    string Message,
    bool IsTerminal,
    int Processed,
    int? Total,
    int? PercentComplete,
    int Discovered,
    int Published,
    int Reviewed,
    int? PollAfterSeconds,
    string? Error,
    DateTime CreatedUtc,
    DateTime? FinishedUtc);

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed record SourceInfo(string Id, string PublisherId, string Url, string Kind, bool Approved,
    bool AllowArticleCapture, string Status, DateTime? LastAttemptUtc, DateTime? LastSuccessUtc, DateTime NextDueUtc);

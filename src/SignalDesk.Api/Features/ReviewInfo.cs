using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed record ReviewInfo(string Id, string DocumentId, string Reason, string State,
    string? ProposalJson, string EvidenceJson, string SourceUrl, DateTime? PublishedUtc);

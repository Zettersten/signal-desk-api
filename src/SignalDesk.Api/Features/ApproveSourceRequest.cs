using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class ApproveSourceRequest
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string Kind { get; set; } = "search";
    public bool Approved { get; set; }
    public bool AllowArticleCapture { get; set; }
    public string RightsAcknowledgement { get; set; } = "";
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class ReviewDecisionRequest
{
    public string Id { get; set; } = "";
    public bool Accept { get; set; }
    public string Note { get; set; } = "";
}

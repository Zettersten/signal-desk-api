using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class CreateRunEndpoint(RunQueue queue) : Endpoint<CreateRunRequest, CollectionRun>
{
    public override void Configure()
    {
        Post("/v1/ingestion/runs");
        AllowAnonymous();
        Description(b => b.Produces<CollectionRun>(202).ProducesProblem(400).ProducesProblem(503));
    }
    public override async Task HandleAsync(CreateRunRequest req, CancellationToken ct)
    {
        var run = await queue.EnqueueAsync(req.SourceId, ct);
        HttpContext.Response.Headers.Location = $"/v1/ingestion/runs/{run.Id}";
        await Send.ResponseAsync(run, 202, ct);
    }
}

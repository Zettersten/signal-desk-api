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
        Description(b => b.WithTags("Ingestion").Produces<CollectionRun>(202).ProducesProblem(400).ProducesProblem(503));
        Summary(s =>
        {
            s.Summary = "Start a collection run for one source";
            s.Description = """
                Queues a run that discovers and processes new articles for one approved source. The request returns immediately with `202 Accepted`.

                **Follow the run:** poll `GET /v1/ingestion/runs/{id}` (the `Location` header gives the URL). `state` moves from `queued` to `running` to `succeeded` or `failed`.

                **Budget:** if the daily budget or query limit is reached, the run is deferred until the next UTC day. It is not dropped.

                **Responses:** `202` with the run, `400` if the source is unknown, `503` if live collection is disabled.
                """;
            s.ExampleRequest = new CreateRunRequest { SourceId = "spacenews" };
            s.ResponseExamples[202] = new CollectionRun
            {
                Id = Guid.Parse("7ae6ca12-10c4-4f14-9dcf-0f75340502f4"),
                SourceId = "spacenews",
                State = "queued",
                CreatedUtc = new DateTime(2026, 11, 26, 1, 6, 41, DateTimeKind.Utc)
            };
        });
    }
    public override async Task HandleAsync(CreateRunRequest req, CancellationToken ct)
    {
        var run = await queue.EnqueueAsync(req.SourceId, ct);
        HttpContext.Response.Headers.Location = $"/v1/ingestion/runs/{run.Id}";
        await Send.ResponseAsync(run, 202, ct);
    }
}

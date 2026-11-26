using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class RetryRunEndpoint(IDbContextFactory<DeskDb> factory, RunQueue queue) : Endpoint<RunIdRequest, CollectionRun>
{
    public override void Configure()
    {
        Post("/v1/ingestion/runs/{id}/retry");
        AllowAnonymous();
        Description(b => b.WithTags("Ingestion").Produces<CollectionRun>(202).ProducesProblem(404).ProducesProblem(409));
        Summary(s =>
        {
            s.Summary = "Retry a failed run";
            s.Description = "Starts a new run for the same source as a run that failed. Only runs in `failed` or `enqueue-failed` can be retried; other states return `409`. Retrying creates a new run, so the original run's record is kept.";
        });
    }
    public override async Task HandleAsync(RunIdRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await db.Runs.FindAsync([req.Id], ct) ?? throw new ApiException(404, "Run not found.");
        if (run.State is not ("failed" or "enqueue-failed")) throw new ApiException(409, "Only a terminal failed run can be retried.");
        await Send.ResponseAsync(await queue.EnqueueAsync(run.SourceId, ct), 202, ct);
    }
}

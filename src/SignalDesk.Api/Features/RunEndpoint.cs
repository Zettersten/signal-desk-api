using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class RunEndpoint(IDbContextFactory<DeskDb> factory) : Endpoint<RunIdRequest, CollectionRun>
{
    public override void Configure()
    {
        Get("/v1/ingestion/runs/{id}");
        AllowAnonymous();
        Description(b => b.WithTags("Ingestion").Produces<CollectionRun>(200).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Get the status of a collection run";
            s.Description = "Returns a run's `state`, its counts (`discovered`, `published`, `reviewed`), and the `error` message if it failed. Poll this until `state` is `succeeded` or `failed`.";
            s.ResponseExamples[200] = new CollectionRun
            {
                Id = Guid.Parse("7ae6ca12-10c4-4f14-9dcf-0f75340502f4"),
                SourceId = "spacenews",
                State = "succeeded",
                CreatedUtc = new DateTime(2026, 11, 26, 1, 6, 41, DateTimeKind.Utc),
                FinishedUtc = new DateTime(2026, 11, 26, 1, 6, 45, DateTimeKind.Utc),
                Discovered = 20,
                Published = 0,
                Reviewed = 0
            };
        });
    }
    public override async Task HandleAsync(RunIdRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await Send.OkAsync(await db.Runs.FindAsync([req.Id], ct) ?? throw new ApiException(404, "Run not found."), ct);
    }
}

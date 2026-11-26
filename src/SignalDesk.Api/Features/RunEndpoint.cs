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
    public override void Configure() { Get("/v1/ingestion/runs/{id}"); AllowAnonymous(); }
    public override async Task HandleAsync(RunIdRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await Send.OkAsync(await db.Runs.FindAsync([req.Id], ct) ?? throw new ApiException(404, "Run not found."), ct);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class AiExecutionsEndpoint(IDbContextFactory<DeskDb> factory) : EndpointWithoutRequest<AiExecutionInfo[]>
{
    public override void Configure() { Get("/v1/ai/executions"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await Send.OkAsync(await db.AiExecutions.AsNoTracking().OrderByDescending(x => x.CreatedUtc).Take(100)
            .Select(x => new AiExecutionInfo(x.Id, x.Provider, x.Operation, x.Model, x.Status, x.CreatedUtc,
                x.ReservedUsd, x.RequestId, x.InputTokens, x.OutputTokens, x.ResolutionNote)).ToArrayAsync(ct), ct);
    }
}

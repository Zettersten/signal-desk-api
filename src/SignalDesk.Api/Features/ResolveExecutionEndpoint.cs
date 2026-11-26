using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class ResolveExecutionEndpoint(IDbContextFactory<DeskDb> factory) : Endpoint<ResolveExecutionRequest>
{
    public override void Configure()
    {
        Post("/v1/ai/executions/{id}/allow-retry");
        AllowAnonymous();
        Summary(s => s.Summary = "Explicitly authorize another potentially billable attempt after reconciling an uncertain provider outcome. Previous reservation remains charged.");
    }
    public override async Task HandleAsync(ResolveExecutionRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.AiExecutions.FindAsync([req.Id], ct);
        if (row is null) { await Send.NotFoundAsync(ct); return; }
        if (row.Status is not ("reserved" or "dispatched" or "failed-or-unknown"))
        { await Send.ResultAsync(TypedResults.Conflict()); return; }
        if (row.CreatedUtc > DateTime.UtcNow.AddMinutes(-5))
        { await Send.ResultAsync(TypedResults.Conflict()); return; }
        row.Status = "retry-authorized";
        row.ResolutionNote = req.Note;
        row.ResolvedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

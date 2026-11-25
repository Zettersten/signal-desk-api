using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed record AiExecutionInfo(string Id, string Provider, string Operation, string Model, string Status,
    DateTime CreatedUtc, decimal ReservedUsd, string? RequestId, long? InputTokens, long? OutputTokens,
    string? ResolutionNote);

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

public sealed class ResolveExecutionRequest
{
    public string Id { get; set; } = "";
    public string Note { get; set; } = "";
}

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
        if (req.Note.Trim().Length is < 20 or > 2000) throw new ApiException(400, "Record the provider reconciliation and authorization for another charge.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.AiExecutions.FindAsync([req.Id], ct) ?? throw new ApiException(404, "Execution not found.");
        if (row.Status is not ("reserved" or "dispatched" or "failed-or-unknown"))
            throw new ApiException(409, "Only an unresolved execution can be reconciled.");
        if (row.CreatedUtc > DateTime.UtcNow.AddMinutes(-5))
            throw new ApiException(409, "The request may still be running; wait for its timeout before reconciliation.");
        row.Status = "retry-authorized";
        row.ResolutionNote = req.Note;
        row.ResolvedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

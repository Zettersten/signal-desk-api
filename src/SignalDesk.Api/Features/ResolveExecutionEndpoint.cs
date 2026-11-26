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
        Description(b => b.WithTags("AI").ProducesProblem(404).ProducesProblem(409));
        Summary(s =>
        {
            s.Summary = "Authorize a retry after reconciling an uncertain AI call";
            s.Description = """
                Use this only after you have checked the provider dashboard and confirmed the outcome of an execution that is `dispatched` or `failed-or-unknown`.

                **What it does:** marks the execution `retry-authorized`, so the same request can be sent again. The earlier reservation stays charged. Nothing is refunded.

                **Rules**
                - The execution must be `reserved`, `dispatched`, or `failed-or-unknown`.
                - The request must be at least 5 minutes old, so it cannot still be running.
                - `note` is required (20-2000 characters). Record what you checked and why another charge is acceptable.

                **Responses:** `204` on success, `404` if the execution does not exist, `409` if it is not in an unresolved state or is too recent.
                """;
            s.ExampleRequest = new ResolveExecutionRequest
            {
                Id = "e784be74cb634c3785a09a139f460434",
                Note = "Checked the OpenAI usage dashboard: no charge was recorded for this request. Authorizing one retry."
            };
        });
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

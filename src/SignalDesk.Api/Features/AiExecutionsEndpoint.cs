using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class AiExecutionsEndpoint(IDbContextFactory<DeskDb> factory) : EndpointWithoutRequest<AiExecutionInfo[]>
{
    public override void Configure()
    {
        Get("/v1/ai/executions");
        AllowAnonymous();
        Description(b => b.WithTags("AI").Produces<AiExecutionInfo[]>(200));
        Summary(s =>
        {
            s.Summary = "List recent AI calls and their cost reservations";
            s.Description = """
                Returns the 100 most recent AI calls, newest first. Use this to check spend and to find calls that need attention.

                **Status values**
                - `reserved`: budget was set aside, but the call has not been sent.
                - `dispatched`: the call was sent and has no recorded result yet.
                - `complete`: the call finished and its result was saved.
                - `failed-or-unknown`: the outcome is uncertain. The provider may still have charged for it.
                - `retry-authorized`: an operator approved another billable attempt.

                `reservedUsd` is an estimate that counts against the daily budget. It is not the provider's bill.
                """;
            s.ResponseExamples[200] = new AiExecutionInfo[]
            {
                new("e784be74cb634c3785a09a139f460434", "openai", "transform", "gpt-4.1-mini", "complete",
                    new DateTime(2026, 11, 26, 1, 12, 42, DateTimeKind.Utc), 0.05m, "chatcmpl-EoIKSA8JwzDv5Ou3pwllug7l8iQgK", 761, 91, null)
            };
        });
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await Send.OkAsync(await db.AiExecutions.AsNoTracking().OrderByDescending(x => x.CreatedUtc).Take(100)
            .Select(x => new AiExecutionInfo(x.Id, x.Provider, x.Operation, x.Model, x.Status, x.CreatedUtc,
                x.ReservedUsd, x.RequestId, x.InputTokens, x.OutputTokens, x.ResolutionNote)).ToArrayAsync(ct), ct);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class SourcesEndpoint(IDbContextFactory<DeskDb> factory) : EndpointWithoutRequest<SourceInfo[]>
{
    public override void Configure()
    {
        Get("/v1/sources");
        AllowAnonymous();
        Description(b => b.WithTags("Sources").Produces<SourceInfo[]>(200));
        Summary(s =>
        {
            s.Summary = "List collection sources";
            s.Description = """
                Returns every configured source with its approval state, schedule, and last result.

                A source only collects when `approved` is `true` and `nextDueUtc` has passed. Use `PATCH /v1/sources/{id}` to approve one.
                """;
        });
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await Send.OkAsync(await db.Sources.OrderBy(x => x.Id).Select(x => new SourceInfo(
            x.Id, x.PublisherId, x.Url, x.Kind, x.Approved, x.AllowArticleCapture, x.Status,
            x.LastAttemptUtc, x.LastSuccessUtc, x.NextDueUtc)).ToArrayAsync(ct), ct);
    }
}

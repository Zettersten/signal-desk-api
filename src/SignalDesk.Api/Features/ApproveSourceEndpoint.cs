using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class ApproveSourceEndpoint(IDbContextFactory<DeskDb> factory) : Endpoint<ApproveSourceRequest>
{
    public override void Configure()
    {
        Patch("/v1/sources/{id}");
        AllowAnonymous();
        Description(b => b.WithTags("Sources").ProducesProblem(400).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Approve or change a source";
            s.Description = """
                Sets whether a source may be collected, and whether article text may be stored.

                **Before you approve:** confirm you have the rights to collect from this publisher. Record the basis in `rightsAcknowledgement` (at least 20 characters). The API stores it with the source.

                **Settings**
                - `kind`: `search` (discovery through Perplexity) or `rss` (a verified feed URL).
                - `approved`: `false` disables collection for this source.
                - `allowArticleCapture`: when `false`, only discovery metadata is kept. This is the safer default.

                **Typical flow:** approve with `allowArticleCapture: false`, then start a run with `POST /v1/ingestion/runs`.

                **Responses:** `204` on success, `400` if the kind or acknowledgement is invalid, `404` if the source does not exist.
                """;
            s.ExampleRequest = new ApproveSourceRequest
            {
                Id = "spacenews",
                Url = "https://spacenews.com",
                Kind = "search",
                Approved = true,
                AllowArticleCapture = false,
                RightsAcknowledgement = "Discovery-only evaluation approved by the account owner on 2026-11-26. Reviewer reference pending."
            };
        });
    }
    public override async Task HandleAsync(ApproveSourceRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var source = await db.Sources.Include(x => x.Publisher).SingleOrDefaultAsync(x => x.Id == req.Id, ct);
        if (source is null) { await Send.NotFoundAsync(ct); return; }
        SafeFetcher.Validate(req.Url, source.Publisher.Website);
        source.Url = req.Url; source.Kind = req.Kind; source.Approved = req.Approved;
        source.AllowArticleCapture = req.AllowArticleCapture && req.Approved;
        source.RightsAcknowledgement = req.RightsAcknowledgement;
        source.Status = req.Approved ? "approved" : "disabled";
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

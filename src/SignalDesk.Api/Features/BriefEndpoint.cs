using FastEndpoints;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class BriefEndpoint(CatalogStore store, IIntelligenceModel model, IDecisionService decisions) : Endpoint<BriefInput, Brief>
{
    public static readonly string[] Modes = ["Strategic brief", "Summarize", "Explain"];
    public static readonly string[] Audiences = ["Strategy leadership", "Business development", "Executive team", "Product and partnerships"];
    private static readonly SemaphoreSlim Slots = new(2);

    public override void Configure()
    {
        Post("/v1/briefs");
        AllowAnonymous();
        Description(b => b.ProducesProblem(400).ProducesProblem(429).ProducesProblem(503).ProducesProblem(504));
        Summary(s => s.Summary = "Synchronous evidence-grounded analysis of 1–20 selected catalog records.");
    }

    public override async Task HandleAsync(BriefInput req, CancellationToken ct)
    {
        if (req.Records is not { Length: >= 1 and <= 20 } || string.IsNullOrWhiteSpace(req.Objective) ||
            req.Objective.Length > 3000 || !Modes.Contains(req.Mode) || !Audiences.Contains(req.Audience) ||
            req.Records.Any(r => r is null || string.IsNullOrWhiteSpace(r.Id)) ||
            req.Records.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != req.Records.Length)
            throw new ApiException(400, "Select 1–20 unique records, an objective of 1–3000 characters, and a supported audience/framing.");
        var catalog = await store.LoadAsync(ct);
        var records = req.Records.Select(r => catalog.Records.FirstOrDefault(x => string.Equals(x.Id, r.Id))
            ?? throw new ApiException(400, "A selected record is not in the current catalog.")).ToArray();
        if (records.Select(r => r.Sample).Distinct().Count() != 1) throw new ApiException(400, "Mixed sample/live evidence is not allowed.");
        if (string.Equals(catalog.Mode, "sample"))
        {
            await Send.OkAsync(new("Sample evidence analysis",
                $"Sample analysis — fictional evaluation data, not verified reporting.\n\n## Objective\n{req.Objective}\n\n## Evidence\n" +
                string.Join("\n\n", records.Select(r => $"[{r.Id}] {r.Title} — {r.Publisher}\n{r.Summary}")) +
                "\n\n## Limitations\nVerify each claim at its original source before making a decision."), ct);
            return;
        }
        if (!await Slots.WaitAsync(0, ct)) throw new ApiException(429, "Analysis is busy; try again shortly.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            var draft = await model.BriefAsync(req with { Records = records }, timeout.Token);
            if (string.IsNullOrWhiteSpace(draft.Title) || string.IsNullOrWhiteSpace(draft.Text) ||
                draft.Text.Length > 16000 || draft.Text.Contains('<') || draft.RecordIds.Length == 0 ||
                draft.RecordIds.Any(id => records.All(r => !string.Equals(r.Id, id))))
            {
                Logger.LogWarning("Brief rejected by structural checks (empty text, markup, or unknown record IDs)");
                throw new ApiException(503, "The draft could not be grounded in the selected evidence.");
            }
            if (!await decisions.SupportsAsync(records, draft, timeout.Token))
            {
                Logger.LogWarning("Brief rejected by the support check; see the TypeSafe support log for rejected statements");
                throw new ApiException(503, "The draft could not be grounded in the selected evidence.");
            }
            await Send.OkAsync(new(draft.Title, draft.Text), ct);
        }
        finally { Slots.Release(); }
    }
}

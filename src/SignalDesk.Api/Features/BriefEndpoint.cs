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
        Description(b => b.WithTags("Briefs").Produces<Brief>(200).ProducesProblem(400).ProducesProblem(429).ProducesProblem(503).ProducesProblem(504));
        Summary(s =>
        {
            s.Summary = "Write an evidence-grounded brief about selected signals";
            s.Description = """
                Writes a short analysis of 1-20 signals you select, and returns it in the same request. The brief cites each fact with a record ID, and the API checks each statement against the evidence before it returns.

                **Choose `audience` and `mode`**
                - `audience`: `Strategy leadership`, `Business development`, `Executive team`, or `Product and partnerships`.
                - `mode`: `Strategic brief` (default), `Summarize`, or `Explain`.

                **Output format:** `text` uses Markdown. Facts are one sentence each and end with a `[record-id]` citation. Commentary is labelled `Interpretation:`, `Evidence gap:`, `Next verification:`, or `Next step:`, so you can tell fact from judgment.

                **Limits:** objective 1-3000 characters. Records must come from the same mode (all sample or all live). The request can take up to 60 seconds.

                **Failures**
                - `400`: invalid input, unknown record, or mixed sample and live records.
                - `429`: two briefs are already running. Retry shortly.
                - `503`: the draft could not be grounded in the selected evidence. Select different records, or narrow the objective.
                - `504`: the model took longer than the time limit.

                **Sample mode** returns a clearly labelled fictional analysis with no provider calls.
                """;
            s.ExampleRequest = new BriefInput(
            [
                new Signal("live-doc-a03c4459a8270a45d6c165f0a5cfe336b8cbccd3d34a3e77a1becd2ae9af1773", "blacksky", "spacenews",
                    "BlackSky", "SpaceNews", "BlackSky Expands with EpochGeo Acquisition",
                    "BlackSky acquired geospatial data firm EpochGeo to expand its real-time intelligence tools.",
                    [new Paragraph("p-0-87b135dd1c4d", "BlackSky Technology announced it acquired geospatial data firm EpochGeo.")],
                    "2026-11-24T14:16:47Z", "Earth Observation", 100, "14:16", "B", "blue", false,
                    "https://spacenews.com/blacksky-acquires-data-firm-epochgeo/")
            ],
            "Summarize the BlackSky acquisition of EpochGeo and what it means for competing suppliers. Label interpretation and evidence gaps.",
            "Strategy leadership",
            "Strategic brief");
            s.ResponseExamples[200] = new Brief(
                "Strategic brief: BlackSky acquires EpochGeo",
                "BlackSky acquired geospatial data firm EpochGeo to expand its real-time intelligence tools [live-doc-a03c4459a8270a45d6c165f0a5cfe336b8cbccd3d34a3e77a1becd2ae9af1773].\n\nInterpretation: The move suggests BlackSky wants to sell analysis as well as imagery.\n\nEvidence gap: The article does not give the deal value or closing date.");
        });
    }

    public override async Task HandleAsync(BriefInput req, CancellationToken ct)
    {
        var catalog = await store.LoadAsync(ct);
        var records = req.Records.Select(r => catalog.Records.FirstOrDefault(x => string.Equals(x.Id, r.Id))
            ?? throw new ApiException(400, "A selected record is not in the current catalog.")).ToArray();
        if (records.Select(r => r.Sample).Distinct().Count() != 1) ThrowError("Mixed sample/live evidence is not allowed.");
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

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class RunStatusEndpoint(IDbContextFactory<DeskDb> factory) : Endpoint<RunIdRequest, RunStatus>
{
    public override void Configure()
    {
        Get("/v1/ingestion/runs/{id}/status");
        AllowAnonymous();
        Description(b => b.WithTags("Ingestion").Produces<RunStatus>(200).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Poll the progress of a collection run";
            s.Description = """
                Returns where a collection run is now, in a shape that is easy to poll.

                **How to poll:** call this every `pollAfterSeconds` seconds while `isTerminal` is `false`. Stop when `isTerminal` is `true`, then read `state`, `published`, `reviewed`, and `error`.

                **Steps, in order**
                1. `evaluating-captured`: re-checking articles captured by earlier runs.
                2. `discovering`: planning queries and searching the source.
                3. `processing`: fetching and evaluating each discovered article. `processed` of `total` shows progress.
                4. `finalizing`: saving counts and the next due time.
                5. `finished`: the run succeeded.

                `state` is the overall outcome: `queued`, `running`, `succeeded`, `failed`, or `enqueue-failed`. `step` says what the running work is doing.

                `percentComplete` is only set during `processing`, when the total number of articles is known. Other steps report `null`.

                **Example:** `GET /v1/ingestion/runs/7ae6ca12-10c4-4f14-9dcf-0f75340502f4/status`
                """;
            s.ResponseExamples[200] = new RunStatus(
                Guid.Parse("7ae6ca12-10c4-4f14-9dcf-0f75340502f4"), "spacenews", "running", "processing",
                "Processing article 3 of 20.", false, 2, 20, 10, 12, 1, 0, 2, null,
                new DateTime(2026, 11, 26, 1, 6, 41, DateTimeKind.Utc), null);
        });
    }

    public override async Task HandleAsync(RunIdRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (run is null) { await Send.NotFoundAsync(ct); return; }

        var terminal = run.State is "succeeded" or "failed" or "enqueue-failed";
        var percent = run.Total is > 0 ? (int)Math.Round(100.0 * run.Processed / run.Total.Value) : (int?)null;
        var message = run.State switch
        {
            "queued" => "Waiting to start.",
            "succeeded" => $"Finished. {run.Published} published, {run.Reviewed} waiting for review.",
            "failed" or "enqueue-failed" => "Failed. See error.",
            _ => run.Step switch
            {
                "evaluating-captured" => "Re-checking articles captured by earlier runs.",
                "discovering" => "Searching the source for new articles.",
                "processing" => run.Total is > 0
                    ? $"Processing article {Math.Min(run.Processed + 1, run.Total.Value)} of {run.Total.Value}."
                    : "Processing articles.",
                "finalizing" => "Saving results.",
                _ => "Working."
            }
        };

        await Send.OkAsync(new RunStatus(
            run.Id, run.SourceId, run.State, run.Step, message, terminal,
            run.Processed, run.Total, percent, run.Discovered, run.Published, run.Reviewed,
            terminal ? null : 2, run.Error, run.CreatedUtc, run.FinishedUtc), ct);
    }
}

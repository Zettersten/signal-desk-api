using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Features;

namespace SignalDesk.Api.Tests;

public sealed class RunStatusTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Status_reports_step_progress_and_poll_hint_while_processing()
    {
        using var factory = new DeskFactory("live");
        using var client = factory.CreateClient();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<DeskDb>>();
        var run = new CollectionRun
        {
            SourceId = "iceye",
            State = "running",
            Step = "processing",
            Processed = 5,
            Total = 20,
            Discovered = 20,
            CreatedUtc = DateTime.UtcNow
        };
        await using (var db = await dbFactory.CreateDbContextAsync(Ct))
        {
            db.Runs.Add(run);
            await db.SaveChangesAsync(Ct);
        }

        var status = await client.GetFromJsonAsync<RunStatus>($"/v1/ingestion/runs/{run.Id}/status", Wire.Json, Ct);

        Assert.NotNull(status);
        Assert.Equal("processing", status.Step);
        Assert.Equal(5, status.Processed);
        Assert.Equal(20, status.Total);
        Assert.Equal(25, status.PercentComplete);
        Assert.Equal("Processing article 6 of 20.", status.Message);
        Assert.False(status.IsTerminal);
        Assert.Equal(2, status.PollAfterSeconds);
    }

    [Fact]
    public async Task Status_is_terminal_once_the_run_has_succeeded()
    {
        using var factory = new DeskFactory("live");
        using var client = factory.CreateClient();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<DeskDb>>();
        var run = new CollectionRun
        {
            SourceId = "iceye",
            State = "succeeded",
            Step = "finished",
            Published = 1,
            Reviewed = 2,
            CreatedUtc = DateTime.UtcNow,
            FinishedUtc = DateTime.UtcNow
        };
        await using (var db = await dbFactory.CreateDbContextAsync(Ct))
        {
            db.Runs.Add(run);
            await db.SaveChangesAsync(Ct);
        }

        var status = await client.GetFromJsonAsync<RunStatus>($"/v1/ingestion/runs/{run.Id}/status", Wire.Json, Ct);

        Assert.NotNull(status);
        Assert.True(status.IsTerminal);
        Assert.Null(status.PollAfterSeconds);
        Assert.Null(status.PercentComplete);
        Assert.Equal("Finished. 1 published, 2 waiting for review.", status.Message);
    }
}

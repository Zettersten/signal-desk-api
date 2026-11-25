using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Data;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.Ingestion;

public sealed record CollectSource(Guid RunId) : ICommand;
public sealed class CollectSourceHandler(SourceCollector collector) : ICommandHandler<CollectSource>
{
    public Task ExecuteAsync(CollectSource command, CancellationToken ct) => collector.CollectAsync(command.RunId, ct);
}

public sealed class JobStorage(IDbContextFactory<DeskDb> factory) : IJobStorageProvider<JobRecord>
{
    public bool DistributedJobProcessingEnabled => true;

    public async Task StoreJobAsync(JobRecord record, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Jobs.Add(record);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ICollection<JobRecord>> GetNextBatchAsync(PendingJobSearchParams<JobRecord> parameters)
    {
        var ct = parameters.CancellationToken;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var records = await db.Jobs.Where(parameters.Match).Take(parameters.Limit).ToArrayAsync(ct);
        foreach (var record in records)
        {
            record.DequeueAfter = DateTime.UtcNow.AddMinutes(10);
            record.State = "running";
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return records;
    }

    public async Task MarkJobAsCompleteAsync(JobRecord record, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Jobs.Where(x => x.TrackingID == record.TrackingID).ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.IsComplete, true).SetProperty(x => x.State, "succeeded"), ct);
    }

    public async Task CancelJobAsync(Guid trackingId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Jobs.Where(x => x.TrackingID == trackingId).ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.IsComplete, true).SetProperty(x => x.State, "cancelled"), ct);
    }

    public async Task OnHandlerExecutionFailureAsync(JobRecord record, Exception exception, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var saved = await db.Jobs.SingleAsync(x => x.TrackingID == record.TrackingID, ct);
        var command = ((IJobStorageRecord)saved).GetCommand<CollectSource>();
        var run = await db.Runs.FindAsync([command.RunId], ct);
        if (exception is ApiException { Status: 429 })
        {
            saved.ExecuteAfter = DateTime.UtcNow.Date.AddDays(1).AddMinutes(1);
            saved.DequeueAfter = saved.ExecuteAfter;
            saved.ExpireOn = saved.ExecuteAfter.AddDays(7);
            saved.State = "deferred";
            if (run is not null) { run.State = "deferred"; run.Error = "Budget or provider rate limit; deferred until next UTC day."; }
            await db.SaveChangesAsync(ct);
            return;
        }
        saved.Attempts++;
        saved.Error = exception.GetType().Name;
        saved.State = saved.Attempts >= 3 ? "failed" : "retry";
        saved.IsComplete = saved.Attempts >= 3;
        saved.ExecuteAfter = DateTime.UtcNow.AddMinutes(Math.Pow(2, saved.Attempts));
        saved.DequeueAfter = saved.ExecuteAfter;
        if (run is not null) { run.State = saved.State; run.Error = saved.Error; }
        await db.SaveChangesAsync(ct);
    }

    public async Task PurgeStaleJobsAsync(StaleJobSearchParams<JobRecord> parameters)
    {
        await using var db = await factory.CreateDbContextAsync(parameters.CancellationToken);
        await db.Jobs.Where(x => !x.IsComplete && x.ExpireOn <= DateTime.UtcNow).ExecuteUpdateAsync(
            s => s.SetProperty(x => x.IsComplete, true).SetProperty(x => x.State, "expired"),
            parameters.CancellationToken);
        await db.Runs.Where(x => x.JobTrackingId != null && x.State != "succeeded" &&
            db.Jobs.Any(j => j.TrackingID == x.JobTrackingId && j.State == "expired"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "failed").SetProperty(x => x.Error, "Job expired; retained for review."),
                parameters.CancellationToken);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Ingestion;

public sealed class RunQueue(IDbContextFactory<DeskDb> factory, IOptions<DeskOptions> options, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1);

    public async Task<CollectionRun> EnqueueAsync(string sourceId, CancellationToken ct)
    {
        if (!options.Value.EnableCollectors || !options.Value.EnableLiveAi || !string.Equals(options.Value.Mode, "live"))
            throw new ApiException(503, "Enable the approved live collection workflow before queueing work.");
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var source = await db.Sources.SingleOrDefaultAsync(x => x.Id == sourceId, ct);
            if (source?.Approved != true)
                throw new ApiException(400, "Choose an approved source.");
            var active = await db.Runs.FirstOrDefaultAsync(x => x.SourceId == sourceId &&
                (x.State == "queued" || x.State == "running" || x.State == "retry" || x.State == "deferred"), ct);
            if (active is not null)
            {
                if (active.JobTrackingId is null)
                {
                    var payload = Wire.Encode(new CollectSource(active.Id));
                    var persisted = await db.Jobs.FirstOrDefaultAsync(x => x.PayloadJson == payload, ct);
                    active.JobTrackingId = persisted?.TrackingID ??
                        await new CollectSource(active.Id).QueueJobAsync(expireOn: DateTime.UtcNow.AddDays(7));
                    await db.SaveChangesAsync(ct);
                }
                return active;
            }
            var run = new CollectionRun { SourceId = sourceId, CreatedUtc = clock.GetUtcNow().UtcDateTime };
            source.NextDueUtc = clock.GetUtcNow().UtcDateTime.AddHours(string.Equals(source.Kind, "rss") ? 1 : 24);
            db.Runs.Add(run);
            await db.SaveChangesAsync(ct);
            try
            {
                run.JobTrackingId = await new CollectSource(run.Id).QueueJobAsync(expireOn: DateTime.UtcNow.AddDays(7));
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                run.State = "enqueue-failed";
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            return run;
        }
        finally { gate.Release(); }
    }
}

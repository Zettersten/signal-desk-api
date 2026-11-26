using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Ingestion;

public sealed class CollectionScheduler(
    IDbContextFactory<DeskDb> factory, RunQueue queue, IOptions<DeskOptions> options, ILogger<CollectionScheduler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableCollectors) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(stoppingToken);
                var due = await db.Sources.Where(x => x.Approved && x.NextDueUtc <= DateTime.UtcNow)
                    .OrderBy(x => x.NextDueUtc).Take(3).Select(x => x.Id).ToArrayAsync(stoppingToken);
                foreach (var id in due) await queue.EnqueueAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Collection scheduling deferred: {Type}", error.GetType().Name); }
        }
    }
}

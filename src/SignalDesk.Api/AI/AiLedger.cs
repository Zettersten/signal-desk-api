using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.AI;

public interface IAiResultMetadata
{
    string? ResolvedModel { get; }
    string? RequestId { get; }
    long? InputTokens { get; }
    long? OutputTokens { get; }
}

public sealed class AiLedger(IDbContextFactory<DeskDb> factory, IOptions<DeskOptions> options, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1);

    public async Task<T> ExecuteAsync<T>(
        string provider, string operation, string model, object input, decimal reserve,
        Func<CancellationToken, Task<T>> call, CancellationToken ct, int units = 1, bool cache = true)
    {
        if (!options.Value.EnableLiveAi || options.Value.Mode != "live")
            throw new ApiException(503, "Live AI is disabled. Sample mode makes no provider calls.");
        if (reserve <= 0 || options.Value.DailyBudgetUsd <= 0)
            throw new ApiException(503, "Configure an approved daily budget and conservative per-call reservations first.");
        var hash = CatalogStore.Hash(Wire.Encode(new { input, model, version = "v1" }));
        AiExecution row;
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var now = clock.GetUtcNow().UtcDateTime;
            if (cache)
            {
                var cached = await db.AiExecutions.AsNoTracking().Where(x =>
                    x.Provider == provider && x.Operation == operation && x.Model == model &&
                    x.InputHash == hash && x.Status == "complete" && x.ResultJson != null)
                    .OrderByDescending(x => x.CreatedUtc).FirstOrDefaultAsync(ct);
                if (cached?.ResultJson is not null) return Wire.Decode<T>(cached.ResultJson);
            }
            if (await db.AiExecutions.AnyAsync(x => x.Provider == provider && x.Operation == operation &&
                x.Model == model && x.InputHash == hash &&
                (x.Status == "reserved" || x.Status == "dispatched" || x.Status == "failed-or-unknown"), ct))
                throw new ApiException(409, "An earlier AI request has an uncertain outcome. Reconcile it in AI executions before explicitly allowing retry.");
            var today = await db.AiExecutions.Where(x => x.CreatedUtc >= now.Date).ToArrayAsync(ct);
            if (today.Sum(x => x.ReservedUsd) + reserve > options.Value.DailyBudgetUsd ||
                (provider == "perplexity" && today.Where(x => x.Provider == provider).Sum(x => x.Units) + units > options.Value.DailySearchQueries) ||
                (operation == "transform" && today.Count(x => x.Operation == operation) >= options.Value.DailyDocuments))
                throw new ApiException(429, "Daily intelligence budget reached; work is deferred.");
            row = new()
            {
                Provider = provider,
                Operation = operation,
                Model = model,
                InputHash = hash,
                CreatedUtc = now,
                ReservedUsd = reserve,
                Units = units
            };
            db.AiExecutions.Add(row);
            await db.SaveChangesAsync(ct);
        }
        finally { gate.Release(); }
        try
        {
            await using (var dispatch = await factory.CreateDbContextAsync(ct))
                await dispatch.AiExecutions.Where(x => x.Id == row.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "dispatched"), ct);
            var result = await call(ct);
            await using var db = await factory.CreateDbContextAsync(ct);
            var saved = await db.AiExecutions.SingleAsync(x => x.Id == row.Id, ct);
            saved.Status = "complete";
            saved.ResultJson = cache ? Wire.Encode(result) : null;
            if (result is IAiResultMetadata metadata)
            {
                saved.RequestId = metadata.RequestId;
                saved.InputTokens = metadata.InputTokens;
                saved.OutputTokens = metadata.OutputTokens;
            }
            await db.SaveChangesAsync(ct);
            return result;
        }
        catch
        {
            await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
            await db.AiExecutions.Where(x => x.Id == row.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "failed-or-unknown"), CancellationToken.None);
            throw;
        }
    }
}

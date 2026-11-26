using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Data;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.Ingestion;

public sealed class CollectSourceHandler(SourceCollector collector) : ICommandHandler<CollectSource>
{
    public Task ExecuteAsync(CollectSource command, CancellationToken ct) => collector.CollectAsync(command.RunId, ct);
}

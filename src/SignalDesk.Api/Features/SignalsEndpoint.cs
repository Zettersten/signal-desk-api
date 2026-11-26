using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class SignalsEndpoint(CatalogStore store) : Endpoint<SignalQuery, SignalPage>
{
    public override void Configure() { Get("/v1/signals"); AllowAnonymous(); }
    public override async Task HandleAsync(SignalQuery req, CancellationToken ct)
    {
        if (req.Limit is < 1 or > 100 || req.MinImportance is < 0 or > 100 || req.Query?.Length > 300)
            throw new ApiException(400, "Invalid query bounds.");
        var records = (await store.LoadAsync(ct)).Records.Where(r =>
            (req.CompetitorId is null || string.Equals(r.CompetitorId, req.CompetitorId)) &&
            (req.PublisherId is null || string.Equals(r.PublisherId, req.PublisherId)) &&
            (req.Category is null || string.Equals(r.Category, req.Category)) &&
            r.Importance >= req.MinImportance &&
            (req.After is null || DateTime.Parse(r.PublishedAt, CultureInfo.InvariantCulture).ToUniversalTime() >= req.After.Value.ToUniversalTime()) &&
            (req.Before is null || DateTime.Parse(r.PublishedAt, CultureInfo.InvariantCulture).ToUniversalTime() <= req.Before.Value.ToUniversalTime()) &&
            (req.Query is null || $"{r.Title} {r.Summary} {r.Company} {r.Publisher}".Contains(req.Query, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var offset = 0;
        if (req.Cursor is not null)
        {
            var index = Array.FindIndex(records, r => string.Equals(r.Id, req.Cursor));
            if (index < 0) throw new ApiException(400, "Cursor is no longer in this result set.");
            offset = index + 1;
        }
        var page = records.Skip(offset).Take(req.Limit).ToArray();
        await Send.OkAsync(new(page, offset + page.Length < records.Length ? page[^1].Id : null), ct);
    }
}

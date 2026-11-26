using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class CatalogEndpoint(CatalogStore store) : EndpointWithoutRequest<Catalog>
{
    public override void Configure()
    {
        Get("/v1/catalog");
        AllowAnonymous();
        Summary(s => s.Summary = "Complete frontend-compatible intelligence catalog; never triggers collection.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var catalog = await store.LoadAsync(ct);
        var etag = $"\"{CatalogStore.Hash(Wire.Encode(catalog))}\"";
        HttpContext.Response.Headers.ETag = etag;
        HttpContext.Response.Headers.CacheControl = "private, no-cache";
        if (HttpContext.Request.Headers.IfNoneMatch.Any(v => string.Equals(v, etag)))
        {
            await Send.ResultAsync(Results.StatusCode(304));
            return;
        }
        await Send.OkAsync(catalog, ct);
    }
}

public sealed class IdRequest
{
    public string Id { get; set; } = "";
}

public sealed class SignalQuery
{
    public string? CompetitorId { get; set; }
    public string? PublisherId { get; set; }
    public string? Category { get; set; }
    public int MinImportance { get; set; }
    public DateTime? After { get; set; }
    public DateTime? Before { get; set; }
    public string? Query { get; set; }
    public string? Cursor { get; set; }
    public int Limit { get; set; } = 50;
}
public sealed record SignalPage(Signal[] Records, string? NextCursor);

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
public sealed class SignalEndpoint(CatalogStore store) : Endpoint<IdRequest, Signal>
{
    public override void Configure() { Get("/v1/signals/{id}"); AllowAnonymous(); }
    public override async Task HandleAsync(IdRequest req, CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Records.FirstOrDefault(x => string.Equals(x.Id, req.Id))
            ?? throw new ApiException(404, "Record not found."), ct);
}
public sealed class CompetitorsEndpoint(CatalogStore store) : EndpointWithoutRequest<Competitor[]>
{
    public override void Configure() { Get("/v1/competitors"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Competitors, ct);
}
public sealed class CompetitorEndpoint(CatalogStore store) : Endpoint<IdRequest, Competitor>
{
    public override void Configure() { Get("/v1/competitors/{id}"); AllowAnonymous(); }
    public override async Task HandleAsync(IdRequest req, CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Competitors.FirstOrDefault(x => string.Equals(x.Id, req.Id))
            ?? throw new ApiException(404, "Competitor not found."), ct);
}
public sealed class ActivityEndpoint(CatalogStore store) : Endpoint<IdRequest, ProfileActivity[]>
{
    public override void Configure() { Get("/v1/competitors/{id}/activity"); AllowAnonymous(); }
    public override async Task HandleAsync(IdRequest req, CancellationToken ct) =>
        await Send.OkAsync(((await store.LoadAsync(ct)).Competitors.FirstOrDefault(x => string.Equals(x.Id, req.Id))
            ?? throw new ApiException(404, "Competitor not found.")).Activity, ct);
}
public sealed class PublishersEndpoint(CatalogStore store) : EndpointWithoutRequest<Publisher[]>
{
    public override void Configure() { Get("/v1/publishers"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Publishers, ct);
}
public sealed class PublisherEndpoint(CatalogStore store) : Endpoint<IdRequest, Publisher>
{
    public override void Configure() { Get("/v1/publishers/{id}"); AllowAnonymous(); }
    public override async Task HandleAsync(IdRequest req, CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Publishers.FirstOrDefault(x => string.Equals(x.Id, req.Id))
            ?? throw new ApiException(404, "Publisher not found."), ct);
}

using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class SignalsEndpoint(CatalogStore store) : Endpoint<SignalQuery, SignalPage>
{
    public override void Configure()
    {
        Get("/v1/signals");
        AllowAnonymous();
        Description(b => b.WithTags("Catalog").Produces<SignalPage>(200).ProducesProblem(400));
        Summary(s =>
        {
            s.Summary = "Search and page through signals";
            s.Description = """
                Returns published signals, newest first, with optional filters.

                **Filters:** `competitorId`, `publisherId`, `category`, `minImportance` (0-100), `after` and `before` (ISO 8601 dates), and `query` (case-insensitive text match, max 300 characters).

                **Paging:** `limit` is 1-100 and defaults to 50. When `nextCursor` is not null, pass it back as `cursor` to get the next page. A `null` cursor means you have reached the end.

                **Cursor expiry:** if the record behind `cursor` no longer matches your filters, the API returns `400`. Start again without a cursor.

                **Example:** `GET /v1/signals?competitorId=blacksky&minImportance=60&limit=10`
                """;
            s.ResponseExamples[200] = new SignalPage(
            [
                new Signal("live-doc-a03c4459a8270a45d6c165f0a5cfe336b8cbccd3d34a3e77a1becd2ae9af1773", "blacksky", "spacenews",
                    "BlackSky", "SpaceNews", "BlackSky Expands with EpochGeo Acquisition",
                    "BlackSky acquired geospatial data firm EpochGeo to expand its real-time intelligence tools.",
                    [new Paragraph("p-0-87b135dd1c4d", "BlackSky Technology announced it acquired geospatial data firm EpochGeo.")],
                    "2026-11-24T14:16:47Z", "Earth Observation", 100, "14:16", "B", "blue", false,
                    "https://spacenews.com/blacksky-acquires-data-firm-epochgeo/")
            ], null);
        });
    }
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

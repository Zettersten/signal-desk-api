using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(DeskDb db, string mode, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        var dataset = await db.Dataset.FindAsync([1], ct);
        if (dataset is not null && dataset.Mode != mode)
            throw new InvalidOperationException("Database mode differs from Desk:Mode. Choose a separate database file.");
        if (dataset is null)
        {
            if (await db.Signals.AnyAsync(ct))
            {
                var existing = Wire.Decode<Signal>((await db.Signals.FirstAsync(ct)).PayloadJson);
                if (existing.Sample != (mode == "sample"))
                    throw new InvalidOperationException("Existing data belongs to another dataset.");
            }
            db.Dataset.Add(new() { Mode = mode });
        }
        var sample = Wire.Decode<Catalog>(await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "data/seeds/sample-catalog.json"), ct));
        if (!await db.Competitors.AnyAsync(ct))
        {
            foreach (var profile in sample.Competitors)
            {
                var live = profile with
                {
                    Overview = "Profile awaiting evidence-backed research.",
                    FoundedYear = null,
                    Incorporated = null,
                    Capabilities = [],
                    Activity = [],
                    Financials = new[] { "Market value", "Earnings", "Revenue", "Funding" }
                        .Select(label => new Financial(label, "No verified coverage yet.")).ToArray(),
                    Logo = null,
                    LogoKind = null,
                    Image = new("/media/profile-placeholder.svg", $"Illustrative placeholder for {profile.Name}",
                        "Illustrative placeholder; not company imagery.")
                };
                db.Competitors.Add(new()
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    AliasesJson = Wire.Encode(new[] { profile.Name, profile.Id }),
                    ProfileJson = Wire.Encode(mode == "sample" ? profile : live)
                });
            }
        }
        if (!await db.Publishers.AnyAsync(ct))
        {
            if (mode == "sample")
                db.Publishers.AddRange(sample.Publishers.Select(p => new PublisherRow
                { Id = p.Id, Name = p.Name, Website = p.Website, ProfileJson = Wire.Encode(p) }));
            else
            {
                var registry = Wire.Decode<SourceSeed[]>(await File.ReadAllTextAsync(
                    Path.Combine(AppContext.BaseDirectory, "data/sources.json"), ct));
                foreach (var seed in registry)
                {
                    var id = CatalogStore.Slug(seed.Publisher);
                    db.Publishers.Add(new()
                    {
                        Id = id,
                        Name = seed.Publisher,
                        Website = seed.Website,
                        TopicsJson = Wire.Encode(seed.Categories),
                        ProfileJson = Wire.Encode(new Publisher(id, seed.Publisher,
                            string.Concat(seed.Publisher.Split(' ').Select(w => w[0])).ToUpperInvariant()[..Math.Min(3, seed.Publisher.Split(' ').Length)],
                            "blue", seed.Website))
                    });
                    db.Sources.Add(new()
                    {
                        Id = id,
                        PublisherId = id,
                        Url = seed.Website,
                        NextDueUtc = DateTime.UtcNow,
                        Status = "pending-approval"
                    });
                }
            }
        }
        if (mode == "sample" && !await db.Signals.AnyAsync(ct))
            db.Signals.AddRange(sample.Records.Select(s => new SignalRow
            {
                Id = s.Id,
                CompetitorId = s.CompetitorId,
                PublisherId = s.PublisherId,
                PublishedUtc = DateTime.Parse(s.PublishedAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
                Importance = s.Importance,
                Category = s.Category,
                PayloadJson = Wire.Encode(s)
            }));
        await db.SaveChangesAsync(ct);
    }
}

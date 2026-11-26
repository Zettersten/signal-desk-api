using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class CatalogStore(IDbContextFactory<DeskDb> factory, IOptions<DeskOptions> options)
{
    public async Task<Catalog> LoadAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var records = await db.Signals.AsNoTracking().OrderByDescending(x => x.PublishedUtc)
            .ThenBy(x => x.Id).Select(x => x.PayloadJson).ToArrayAsync(ct);
        var competitors = await db.Competitors.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct);
        var activity = await db.Activities.AsNoTracking().OrderByDescending(x => x.CreatedUtc).ToArrayAsync(ct);
        var publishers = await db.Publishers.AsNoTracking().OrderBy(x => x.Name).Select(x => x.ProfileJson).ToArrayAsync(ct);
        var profiles = competitors.Select(row =>
        {
            var profile = Wire.Decode<Competitor>(row.ProfileJson);
            return profile with
            {
                Activity = [.. activity.Where(a => string.Equals(a.CompetitorId, row.Id))
                    .Select(a => Wire.Decode<ProfileActivity>(a.PayloadJson)), .. profile.Activity]
            };
        }).ToArray();
        await transaction.CommitAsync(ct);
        return new(records.Select(Wire.Decode<Signal>).ToArray(), profiles,
            publishers.Select(Wire.Decode<Publisher>).ToArray(), options.Value.Mode);
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Slug(string value)
    {
        var letters = value.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ').ToArray();
        return string.Join('-', new string(letters).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

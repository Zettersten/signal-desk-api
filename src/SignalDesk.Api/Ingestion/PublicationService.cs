using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Ingestion;

public sealed record PublicationProposal(SignalDraft Draft, EvidenceAssessment Assessment);

public sealed class PublicationService(IDbContextFactory<DeskDb> factory, TimeProvider clock)
{
    public static bool ValidDraft(SignalDraft draft, Paragraph[] paragraphs, Competitor[] companies) =>
        !string.IsNullOrWhiteSpace(draft.Title) && draft.Title.Length <= 500 &&
        !string.IsNullOrWhiteSpace(draft.Summary) && draft.Summary.Length <= 3000 &&
        !draft.Summary.Contains('<') && !draft.Title.Contains('<') && companies.Any(c => c.Id == draft.CompetitorId) &&
        draft.ParagraphIds is { Length: > 0 } && draft.ParagraphIds.All(id => paragraphs.Any(p => p.Id == id)) &&
        draft.Facts is { Length: <= 20 } && draft.Facts.All(f => f is not null &&
            f.Kind is "capability" or "financial" or "foundedYear" or "incorporated" or "overview" &&
            !string.IsNullOrWhiteSpace(f.Label) && !string.IsNullOrWhiteSpace(f.Value) &&
            !string.IsNullOrWhiteSpace(f.Quote) && f.Value.Length <= 4000 &&
            (f.Kind != "financial" || new[] { "Market value", "Earnings", "Revenue", "Funding" }.Contains(f.Label)) &&
            paragraphs.Any(p => p.Id == f.ParagraphId && p.Text.Contains(f.Quote, StringComparison.Ordinal)));

    public async Task ReviewAsync(DocumentRow document, string reason, PublicationProposal? proposal, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Reviews.AnyAsync(x => x.DocumentId == document.Id, ct))
            db.Reviews.Add(new()
            {
                Id = $"review-{document.Id}",
                DocumentId = document.Id,
                Reason = reason,
                ProposalJson = proposal is null ? null : Wire.Encode(proposal),
                CreatedUtc = clock.GetUtcNow().UtcDateTime
            });
        var saved = await db.Documents.FindAsync([document.Id], ct);
        if (saved is not null) saved.State = "review";
        await db.SaveChangesAsync(ct);
    }

    public async Task PublishAsync(string documentId, PublicationProposal proposal, bool approveFacts, CancellationToken ct,
        string? reviewId = null, string? reviewer = null, string? note = null)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        ReviewRow? review = null;
        if (reviewId is not null)
        {
            review = await db.Reviews.SingleOrDefaultAsync(x => x.Id == reviewId && x.DocumentId == documentId, ct)
                ?? throw new ApiException(404, "Review not found.");
            if (review.State != "pending") throw new ApiException(409, "Review has already been decided.");
        }
        var document = await db.Documents.Include(d => d.Source).ThenInclude(s => s.Publisher)
            .SingleAsync(x => x.Id == documentId, ct);
        if (document.PublishedUtc is null || document.PublishedUtc > clock.GetUtcNow().UtcDateTime.AddMinutes(5))
            throw new ApiException(400, "A supported publication timestamp is required.");
        var companies = (await db.Competitors.ToArrayAsync(ct)).Select(c => Wire.Decode<Competitor>(c.ProfileJson)).ToArray();
        var paragraphs = Wire.Decode<Paragraph[]>(document.ParagraphsJson);
        var draft = proposal.Draft;
        if (!ValidDraft(draft, paragraphs, companies) || !Wire.Categories.Contains(proposal.Assessment.Category))
            throw new ApiException(400, "Proposal lacks valid identity, content, or exact evidence.");
        if (await db.Signals.AnyAsync(s => s.DocumentId == documentId, ct))
        {
            if (review is not null) throw new ApiException(409, "Evidence has already been published.");
            return;
        }
        var company = companies.Single(c => c.Id == draft.CompetitorId);
        var date = DateTime.SpecifyKind(document.PublishedUtc.Value, DateTimeKind.Utc);
        var age = (clock.GetUtcNow().UtcDateTime - date).TotalDays;
        var importance = Math.Clamp(25 + proposal.Assessment.Significance * 20 + (age <= 2 ? 15 : age <= 7 ? 5 : 0), 0, 100);
        var signal = new Signal($"live-{document.Id}", company.Id, document.Source.PublisherId,
            company.Name, document.Source.Publisher.Name, draft.Title, draft.Summary, paragraphs,
            date.ToString("O"), proposal.Assessment.Category, importance, date.ToString("HH:mm"),
            company.Mark, company.Tone, false, document.CanonicalUrl);
        db.Signals.Add(new()
        {
            Id = signal.Id,
            CompetitorId = company.Id,
            PublisherId = signal.PublisherId,
            DocumentId = documentId,
            PublishedUtc = date,
            Category = signal.Category,
            Importance = importance,
            PayloadJson = Wire.Encode(signal)
        });
        if (approveFacts)
        {
            List<FactProposal> currentFacts = [];
            foreach (var fact in draft.Facts)
            {
                if (!await db.Facts.AnyAsync(f => f.CompetitorId == company.Id && f.Kind == fact.Kind &&
                    f.Label == fact.Label && f.Approved && f.Document.PublishedUtc > date, ct))
                    currentFacts.Add(fact);
                db.Facts.Add(new()
                {
                    CompetitorId = company.Id,
                    DocumentId = documentId,
                    Kind = fact.Kind,
                    Label = fact.Label,
                    Value = fact.Value,
                    Currency = fact.Currency,
                    Period = fact.Period,
                    EvidenceJson = Wire.Encode(new { fact.ParagraphId, fact.Quote }),
                    Approved = true
                });
            }
            var financials = company.Financials.ToArray();
            foreach (var fact in currentFacts.Where(f => f.Kind == "financial"))
            {
                var index = Array.FindIndex(financials, f => f.Label == fact.Label);
                if (index >= 0) financials[index] = new(fact.Label,
                    $"Reviewed source: {document.CanonicalUrl}; as of {date:yyyy-MM-dd}.", fact.Value, fact.Period);
            }
            var capabilities = company.Capabilities.Concat(currentFacts.Where(f => f.Kind == "capability")
                .Select(f => new Capability(f.Label, signal.Category, "Watch", f.Value))).DistinctBy(c => c.Name).ToArray();
            var next = company with { Financials = financials, Capabilities = capabilities };
            var overview = currentFacts.FirstOrDefault(f => f.Kind == "overview");
            var founded = currentFacts.FirstOrDefault(f => f.Kind == "foundedYear");
            var incorporated = currentFacts.FirstOrDefault(f => f.Kind == "incorporated");
            if (overview is not null) next = next with { Overview = overview.Value };
            if (founded is not null && int.TryParse(founded.Value, out var year) && year is >= 1600 and <= 2100)
                next = next with { FoundedYear = founded.Value };
            if (incorporated is not null && DateOnly.TryParseExact(incorporated.Value, "yyyy-MM-dd", out _))
                next = next with { Incorporated = new(incorporated.Value, "Reviewed legal incorporation evidence.", document.CanonicalUrl) };
            var row = await db.Competitors.FindAsync([company.Id], ct);
            row!.ProfileJson = Wire.Encode(next);
        }
        db.Activities.Add(new()
        {
            Id = $"activity-{signal.Id}",
            CompetitorId = company.Id,
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
            PayloadJson = Wire.Encode(new ProfileActivity(date.ToString("yyyy-MM-dd"),
                approveFacts && draft.Facts.Length > 0 ? "Reviewed profile evidence added" : "Evidence added",
                signal.Title, signal.Id))
        });
        document.State = "published";
        if (review is not null)
        {
            review.State = "accepted";
            review.DecidedUtc = clock.GetUtcNow().UtcDateTime;
            review.DecidedBy = reviewer;
            review.DecisionNote = note;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

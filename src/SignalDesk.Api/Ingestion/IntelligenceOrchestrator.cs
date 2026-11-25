using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Ingestion;

public sealed class IntelligenceOrchestrator(
    CatalogStore catalog, IDecisionService decisions, IIntelligenceModel model,
    PublicationService publication, IDbContextFactory<DeskDb> factory)
{
    public async Task ProcessAsync(DocumentRow document, CancellationToken ct)
    {
        var companies = (await catalog.LoadAsync(ct)).Competitors;
        Func<DocumentRow, CancellationToken, ValueTask<PublicationProposal?>> assess = async (doc, token) =>
        {
            var paragraphs = Wire.Decode<Paragraph[]>(doc.ParagraphsJson);
            if (paragraphs.Length == 0) { await publication.ReviewAsync(doc, "No permitted article text.", null, token); return null; }
            var assessment = await decisions.AssessAsync(paragraphs, companies, token);
            if (assessment.Outcome == "irrelevant")
            {
                await using var db = await factory.CreateDbContextAsync(token);
                await db.Documents.Where(x => x.Id == doc.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "irrelevant"), token);
                return null;
            }
            if (assessment.Outcome != "candidate")
            { await publication.ReviewAsync(doc, "Ambiguous relevance or company association.", null, token); return null; }
            var draft = await model.TransformAsync(doc, companies, token);
            return new(draft, assessment);
        };
        Func<PublicationProposal?, CancellationToken, ValueTask<string>> verify = async (proposal, token) =>
        {
            if (proposal is null) return "held";
            var paragraphs = Wire.Decode<Paragraph[]>(document.ParagraphsJson);
            if (!PublicationService.ValidDraft(proposal.Draft, paragraphs, companies) ||
                !proposal.Assessment.Companies.TryGetValue(proposal.Draft.CompetitorId, out var association) || association < .7 ||
                !await decisions.SupportsAsync(paragraphs, proposal.Draft, token))
            { await publication.ReviewAsync(document, "Claim support or identity validation failed.", proposal, token); return "review"; }
            if (document.PublishedUtc is null || proposal.Draft.Facts.Length > 0)
            { await publication.ReviewAsync(document, "Profile facts or publication date require review.", proposal, token); return "review"; }
            await publication.PublishAsync(document.Id, proposal, false, token);
            return "published";
        };
        var screen = assess.BindAsExecutor("screen-and-transform");
        var check = verify.BindAsExecutor("verify-and-publish");
        var workflow = new WorkflowBuilder(screen).AddEdge(screen, check).WithOutputFrom(check).Build();
        await InProcessExecution.RunAsync(workflow, document, cancellationToken: ct);
    }
}

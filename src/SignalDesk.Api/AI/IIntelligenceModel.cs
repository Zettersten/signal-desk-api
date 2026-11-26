using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.ClientModel.Primitives;
using OpenAI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.AI;

public interface IIntelligenceModel
{
    Task<ResearchQueries> PlanAsync(string publisher, string[] topics, string[] companies, CancellationToken ct);
    Task<SignalDraft> TransformAsync(DocumentRow document, Competitor[] companies, CancellationToken ct);
    Task<BriefDraft> BriefAsync(BriefInput input, CancellationToken ct);
}

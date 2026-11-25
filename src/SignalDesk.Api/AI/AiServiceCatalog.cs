using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.AI;

public sealed record AiServiceDescriptor(string Id, string Provider, string Responsibility, string? Model, bool Configured, bool Enabled);

public sealed class AiServiceCatalog(IConfiguration configuration, IOptions<DeskOptions> desk, IOptions<AiOptions> ai)
{
    public AiServiceDescriptor[] List() =>
    [
        Describe("perplexity-search", "perplexity", "Ranked domain-filtered source discovery", "PERPLEXITY_API_KEY"),
        Describe("typesafe-decisions", "typesafe", "Typed relevance, classification and claim-support judgments", "TYPESAFE_API_KEY", ai.Value.TypeSafeModel),
        Describe("openai-research-planner", "openai", "Bounded research queries over approved sources", "OPENAI_API_KEY", ai.Value.PlannerModel),
        Describe("openai-transformer", "openai", "Evidence-cited structured signal and profile proposals", "OPENAI_API_KEY", ai.Value.TransformerModel),
        Describe("openai-briefs", "openai", "Grounded selected-record analysis", "OPENAI_API_KEY", ai.Value.BriefModel),
        new("intelligence-orchestrator", "Microsoft Agent Framework", "Typed workflow; durable state in EF", null, true, desk.Value.EnableCollectors)
    ];

    private AiServiceDescriptor Describe(string id, string provider, string purpose, string key, string? model = null) =>
        new(id, provider, purpose, model,
            !string.IsNullOrWhiteSpace(configuration[key]) && (model is null || !string.IsNullOrWhiteSpace(model)),
            desk.Value.EnableLiveAi);
}

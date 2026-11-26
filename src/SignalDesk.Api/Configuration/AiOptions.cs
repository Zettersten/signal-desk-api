namespace SignalDesk.Api.Configuration;

public sealed class AiOptions
{
    public string TypeSafeModel { get; set; } = "jev-1.14.0";
    public string PlannerModel { get; set; } = "";
    public string TransformerModel { get; set; } = "";
    public string BriefModel { get; set; } = "";
    public decimal SearchQueryReserveUsd { get; set; }
    public decimal DecisionReserveUsd { get; set; }
    public decimal OpenAiReserveUsd { get; set; }
}

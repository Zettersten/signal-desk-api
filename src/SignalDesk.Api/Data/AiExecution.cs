using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class AiExecution
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Provider { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Model { get; set; } = "";
    public string Version { get; set; } = "v1";
    public string InputHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public string Status { get; set; } = "reserved";
    public decimal ReservedUsd { get; set; }
    public int Units { get; set; } = 1;
    public string? RequestId { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public string? ResultJson { get; set; }
    public string? ResolutionNote { get; set; }
    public string? ResolvedBy { get; set; }
}

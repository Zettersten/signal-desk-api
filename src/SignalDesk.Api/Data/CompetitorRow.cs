using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class CompetitorRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AliasesJson { get; set; } = "[]";
    public string ProfileJson { get; set; } = "";
}

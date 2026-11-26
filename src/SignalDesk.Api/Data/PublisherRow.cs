using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class PublisherRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Website { get; set; } = "";
    public string TopicsJson { get; set; } = "[]";
    public string ProfileJson { get; set; } = "";
}

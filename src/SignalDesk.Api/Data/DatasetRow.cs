using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class DatasetRow
{
    public int Id { get; set; } = 1;
    public string Mode { get; set; } = "";
}

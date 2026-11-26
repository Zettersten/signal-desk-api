using System.ComponentModel.DataAnnotations.Schema;
using FastEndpoints;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Data;

public sealed class JobRecord : IJobStorageRecord
{
    public Guid TrackingID { get; set; }
    public string QueueID { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTime ExecuteAfter { get; set; }
    public DateTime ExpireOn { get; set; }
    public DateTime DequeueAfter { get; set; }
    public bool IsComplete { get; set; }
    public int Attempts { get; set; }
    public string State { get; set; } = "pending";
    public string? Error { get; set; }
    [NotMapped] public object Command { get; set; } = null!;
    TCommand IJobStorageRecord.GetCommand<TCommand>() => Wire.Decode<TCommand>(PayloadJson);
    void IJobStorageRecord.SetCommand<TCommand>(TCommand command) => PayloadJson = Wire.Encode(command);
}

namespace SignalDesk.Api.Configuration;

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

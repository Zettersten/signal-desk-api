using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record ProfileImage(string Src, string Alt, string Caption);

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Linq;
using AngleSharp.Html.Parser;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Ingestion;

internal static class HostExtensions
{
    public static string Removeprefix(this string host) => host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
}

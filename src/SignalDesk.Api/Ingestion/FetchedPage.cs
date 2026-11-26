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

public sealed record FetchedPage(string Text, string MediaType);

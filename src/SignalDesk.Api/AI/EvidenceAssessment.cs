using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.AI;

public sealed record EvidenceAssessment(double Relevant, double FitsDomain, string Category, int Significance, IReadOnlyDictionary<string, double> Companies)
{
    public string Outcome => Relevant <= .3 || FitsDomain <= .3 ? "irrelevant" :
        Relevant >= .7 && FitsDomain >= .7 && Companies.Values.Any(v => v >= .7) ? "candidate" : "review";
}

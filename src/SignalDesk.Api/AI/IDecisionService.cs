using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.AI;

public interface IDecisionService
{
    Task<EvidenceAssessment> AssessAsync(Paragraph[] evidence, Competitor[] companies, CancellationToken ct);
    Task<bool> SupportsAsync(object evidence, object claims, CancellationToken ct);
}

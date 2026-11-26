using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Ingestion;

public sealed record PublicationProposal(SignalDraft Draft, EvidenceAssessment Assessment);

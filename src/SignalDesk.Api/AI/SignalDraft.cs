using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.ClientModel.Primitives;
using OpenAI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.AI;

public sealed record SignalDraft(string Title, string Summary, string CompetitorId, string[] ParagraphIds, FactProposal[] Facts);

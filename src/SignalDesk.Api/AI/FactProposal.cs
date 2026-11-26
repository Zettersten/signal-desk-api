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

public sealed record FactProposal(string Kind, string Label, string Value, string? Currency, string? Period, string ParagraphId, string Quote);

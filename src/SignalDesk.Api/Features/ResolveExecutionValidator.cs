using FastEndpoints;
using FluentValidation;

namespace SignalDesk.Api.Features;

public sealed class ResolveExecutionValidator : Validator<ResolveExecutionRequest>
{
    public ResolveExecutionValidator()
    {
        RuleFor(x => x.Note)
            .Must(n => n.Trim().Length is >= 20 and <= 2000)
            .WithMessage("Record the provider reconciliation and authorization for another charge.");
    }
}

using FastEndpoints;
using FluentValidation;

namespace SignalDesk.Api.Features;

public sealed class ReviewDecisionValidator : Validator<ReviewDecisionRequest>
{
    public ReviewDecisionValidator()
    {
        RuleFor(x => x.Note)
            .Must(n => n.Trim().Length >= 10)
            .WithMessage("Record a review rationale.");
    }
}

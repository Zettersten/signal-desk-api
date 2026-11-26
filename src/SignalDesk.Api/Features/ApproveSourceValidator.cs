using FastEndpoints;
using FluentValidation;

namespace SignalDesk.Api.Features;

public sealed class ApproveSourceValidator : Validator<ApproveSourceRequest>
{
    public ApproveSourceValidator()
    {
        RuleFor(x => x.Kind)
            .Must(k => k is "search" or "rss")
            .WithMessage("Choose search or rss.");
        RuleFor(x => x.RightsAcknowledgement)
            .Must(r => r.Trim().Length >= 20)
            .WithMessage("Record the source access and redistribution approval.");
    }
}

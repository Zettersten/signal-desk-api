using FastEndpoints;
using FluentValidation;
using SignalDesk.Api.Contracts;

namespace SignalDesk.Api.Features;

public sealed class BriefInputValidator : Validator<BriefInput>
{
    public BriefInputValidator()
    {
        RuleFor(x => x.Records)
            .Must(r => r is { Length: >= 1 and <= 20 } && r.All(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)) &&
                r.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == r.Length)
            .WithMessage("Select 1–20 unique records.");
        RuleFor(x => x.Objective)
            .Must(o => !string.IsNullOrWhiteSpace(o) && o.Length <= 3000)
            .WithMessage("Objective must be 1–3000 characters.");
        RuleFor(x => x.Audience)
            .Must(a => BriefEndpoint.Audiences.Contains(a))
            .WithMessage("Choose a supported audience.");
        RuleFor(x => x.Mode)
            .Must(m => BriefEndpoint.Modes.Contains(m))
            .WithMessage("Choose a supported framing.");
    }
}

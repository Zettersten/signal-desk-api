using FastEndpoints;
using FluentValidation;

namespace SignalDesk.Api.Features;

public sealed class CreateRunValidator : Validator<CreateRunRequest>
{
    public CreateRunValidator()
    {
        RuleFor(x => x.SourceId)
            .Must(id => !string.IsNullOrWhiteSpace(id))
            .WithMessage("Choose a source to collect.");
    }
}

using FluentValidation;
using TourismHcm.Contracts.Narration;
using TourismHcm.Domain.Constants;

namespace TourismHcm.Application.Validators;

public sealed class TriggerRequestValidator : AbstractValidator<TriggerRequest>
{
    public TriggerRequestValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180);

        RuleFor(x => x.Lang)
            .NotEmpty()
            .Must(AppLanguages.IsSupported)
            .WithMessage("Lang must be one of: " + string.Join(", ", AppLanguages.Supported));

        RuleFor(x => x.ClientId)
            .MaximumLength(200);
    }
}

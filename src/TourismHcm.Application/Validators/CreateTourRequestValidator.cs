using FluentValidation;
using TourismHcm.Contracts.Tours;

namespace TourismHcm.Application.Validators;

public sealed class CreateTourRequestValidator : AbstractValidator<CreateTourRequest>
{
    public CreateTourRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PlaceIds)
            .NotEmpty()
            .WithMessage("At least one place is required to create a tour.")
            .Must(ids => ids.Count <= 30)
            .WithMessage("A tour can contain at most 30 places.");

        RuleFor(x => x.StartLatitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.StartLatitude is not null);

        RuleFor(x => x.StartLongitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.StartLongitude is not null);
    }
}

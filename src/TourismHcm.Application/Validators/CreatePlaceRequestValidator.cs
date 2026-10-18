using FluentValidation;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Enums;

namespace TourismHcm.Application.Validators;

public sealed class CreatePlaceRequestValidator : AbstractValidator<CreatePlaceRequest>
{
    public CreatePlaceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DescriptionVi)
            .NotEmpty()
            .MaximumLength(10000);

        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(c => Enum.TryParse<PlaceCategory>(c, true, out _))
            .WithMessage("Category must be one of: " + string.Join(", ", Enum.GetNames<PlaceCategory>()));

        RuleFor(x => x.District)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180);

        RuleFor(x => x.TriggerRadiusMeters)
            .InclusiveBetween(1, 2000)
            .When(x => x.TriggerRadiusMeters is not null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2000);

        RuleFor(x => x.Tags)
            .MaximumLength(500);
    }
}

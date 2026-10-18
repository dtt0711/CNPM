using FluentValidation;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Enums;

namespace TourismHcm.Application.Validators;

public sealed class UpdatePlaceRequestValidator : AbstractValidator<UpdatePlaceRequest>
{
    public UpdatePlaceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .When(x => x.Name is not null);

        RuleFor(x => x.DescriptionVi)
            .NotEmpty()
            .MaximumLength(10000)
            .When(x => x.DescriptionVi is not null);

        RuleFor(x => x.Category)
            .Must(c => Enum.TryParse<PlaceCategory>(c, true, out _))
            .WithMessage("Category must be one of: " + string.Join(", ", Enum.GetNames<PlaceCategory>()))
            .When(x => x.Category is not null);

        RuleFor(x => x.District)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.District is not null);

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(300)
            .When(x => x.Address is not null);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Latitude is not null);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Longitude is not null);

        RuleFor(x => x.TriggerRadiusMeters)
            .InclusiveBetween(1, 2000)
            .When(x => x.TriggerRadiusMeters is not null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2000);

        RuleFor(x => x.Tags)
            .MaximumLength(500);

        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<PlaceStatus>(s, true, out _))
            .WithMessage("Status must be one of: " + string.Join(", ", Enum.GetNames<PlaceStatus>()))
            .When(x => x.Status is not null);
    }
}

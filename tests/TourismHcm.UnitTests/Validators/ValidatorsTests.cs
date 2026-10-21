using FluentAssertions;
using TourismHcm.Application.Validators;
using TourismHcm.Contracts.Auth;
using TourismHcm.Contracts.Narration;
using TourismHcm.Contracts.Places;
using TourismHcm.Contracts.Reviews;
using TourismHcm.Contracts.Tours;

namespace TourismHcm.UnitTests.Validators;

/// <summary>
/// Exercises every FluentValidation validator registered by the Application layer with one
/// representative valid case and the key invalid cases for each rule.
/// </summary>
public sealed class ValidatorsTests
{
    private static CreatePlaceRequest ValidCreatePlaceRequest() => new()
    {
        Name = "Valid Place",
        DescriptionVi = "A valid description.",
        Category = "Museum",
        Ward = "Bến Thành",
        Address = "123 Street",
        Latitude = 10,
        Longitude = 106,
    };

    [Fact]
    public void CreatePlaceRequestValidator_WhenValid_Passes() =>
        new CreatePlaceRequestValidator().Validate(ValidCreatePlaceRequest()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("", "Museum", 10, 106)]
    [InlineData("Name", "NotACategory", 10, 106)]
    [InlineData("Name", "Museum", 999, 106)]
    [InlineData("Name", "Museum", 10, 999)]
    public void CreatePlaceRequestValidator_WhenInvalid_Fails(string name, string category, double lat, double lng)
    {
        var request = ValidCreatePlaceRequest();
        var mutated = new CreatePlaceRequest
        {
            Name = name,
            DescriptionVi = request.DescriptionVi,
            Category = category,
            Ward = request.Ward,
            Address = request.Address,
            Latitude = lat,
            Longitude = lng,
        };

        new CreatePlaceRequestValidator().Validate(mutated).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdatePlaceRequestValidator_WhenAllFieldsNull_Passes() =>
        new UpdatePlaceRequestValidator().Validate(new UpdatePlaceRequest()).IsValid.Should().BeTrue();

    [Fact]
    public void UpdatePlaceRequestValidator_WhenStatusInvalid_Fails() =>
        new UpdatePlaceRequestValidator().Validate(new UpdatePlaceRequest { Status = "NotAStatus" })
            .IsValid.Should().BeFalse();

    [Fact]
    public void UpdatePlaceRequestValidator_WhenStatusValid_Passes() =>
        new UpdatePlaceRequestValidator().Validate(new UpdatePlaceRequest { Status = "Published" })
            .IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void CreateReviewRequestValidator_WhenRatingOutOfRange_Fails(int rating) =>
        new CreateReviewRequestValidator()
            .Validate(new CreateReviewRequest { PlaceId = Guid.NewGuid(), Rating = rating })
            .IsValid.Should().BeFalse();

    [Fact]
    public void CreateReviewRequestValidator_WhenValid_Passes() =>
        new CreateReviewRequestValidator()
            .Validate(new CreateReviewRequest { PlaceId = Guid.NewGuid(), Rating = 3, Comment = "Ok" })
            .IsValid.Should().BeTrue();

    [Fact]
    public void CreateTourRequestValidator_WhenNoPlaceIds_Fails() =>
        new CreateTourRequestValidator()
            .Validate(new CreateTourRequest { Name = "Tour", PlaceIds = [] })
            .IsValid.Should().BeFalse();

    [Fact]
    public void CreateTourRequestValidator_WhenTooManyPlaceIds_Fails() =>
        new CreateTourRequestValidator()
            .Validate(new CreateTourRequest { Name = "Tour", PlaceIds = Enumerable.Range(0, 31).Select(_ => Guid.NewGuid()).ToList() })
            .IsValid.Should().BeFalse();

    [Fact]
    public void CreateTourRequestValidator_WhenValid_Passes() =>
        new CreateTourRequestValidator()
            .Validate(new CreateTourRequest { Name = "Tour", PlaceIds = [Guid.NewGuid()] })
            .IsValid.Should().BeTrue();

    [Theory]
    [InlineData("not-an-email", "Password1")]
    [InlineData("user@example.com", "")]
    public void LoginRequestValidator_WhenInvalid_Fails(string email, string password) =>
        new LoginRequestValidator().Validate(new LoginRequest { Email = email, Password = password })
            .IsValid.Should().BeFalse();

    [Fact]
    public void LoginRequestValidator_WhenValid_Passes() =>
        new LoginRequestValidator().Validate(new LoginRequest { Email = "user@example.com", Password = "secret" })
            .IsValid.Should().BeTrue();

    [Theory]
    [InlineData("short1")]
    [InlineData("alllettersnonumber")]
    [InlineData("12345678")]
    public void RegisterRequestValidator_WhenPasswordInvalid_Fails(string password) =>
        new RegisterRequestValidator()
            .Validate(new RegisterRequest { Email = "user@example.com", Password = password, DisplayName = "User" })
            .IsValid.Should().BeFalse();

    [Fact]
    public void RegisterRequestValidator_WhenValid_Passes() =>
        new RegisterRequestValidator()
            .Validate(new RegisterRequest { Email = "user@example.com", Password = "Password1", DisplayName = "User" })
            .IsValid.Should().BeTrue();

    [Fact]
    public void TriggerRequestValidator_WhenLangUnsupported_Fails() =>
        new TriggerRequestValidator()
            .Validate(new TriggerRequest { Latitude = 10, Longitude = 106, Lang = "xx" })
            .IsValid.Should().BeFalse();

    [Fact]
    public void TriggerRequestValidator_WhenValid_Passes() =>
        new TriggerRequestValidator()
            .Validate(new TriggerRequest { Latitude = 10, Longitude = 106, Lang = "en" })
            .IsValid.Should().BeTrue();
}

using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class PlaceServiceTests
{
    private readonly IPlaceRepository _placeRepository = Substitute.For<IPlaceRepository>();
    private readonly IAudioAssetRepository _audioAssetRepository = Substitute.For<IAudioAssetRepository>();
    private readonly IAudioService _audioService = Substitute.For<IAudioService>();
    private readonly IUnitOfWork _unitOfWork = FakeProviders.CreateUnitOfWork();

    public PlaceServiceTests()
    {
        _audioAssetRepository.GetByPlaceIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<AudioAsset>)[]);
    }

    private PlaceService CreateSut() => new(_placeRepository, _audioAssetRepository, _audioService, _unitOfWork);

    [Fact]
    public async Task GetByIdAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdWithTranslationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Place?)null);

        var sut = CreateSut();
        var act = async () => await sut.GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_OnlyIncludesReadyAudioLanguages()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdWithTranslationsAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var assets = new List<AudioAsset>
        {
            EntityFactory.CreateAudioAsset(place.Id, "en", status: AudioAssetStatus.Ready),
            EntityFactory.CreateAudioAsset(place.Id, "ja", status: AudioAssetStatus.Pending),
            EntityFactory.CreateAudioAsset(place.Id, "ko", status: AudioAssetStatus.Ready),
        };
        _audioAssetRepository.GetByPlaceIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(assets);

        var sut = CreateSut();
        var result = await sut.GetByIdAsync(place.Id);

        result.AvailableAudioLanguages.Should().BeEquivalentTo(["en", "ko"]);
    }

    [Fact]
    public async Task GetNearbyAsync_SortsResultsByDistanceAscending_Haversine()
    {
        var origin = (Lat: 10.0, Lng: 106.0);
        var far = EntityFactory.CreatePlace(name: "Far", latitude: 10.01, longitude: 106.0);
        var near = EntityFactory.CreatePlace(name: "Near", latitude: 10.001, longitude: 106.0);
        var mid = EntityFactory.CreatePlace(name: "Mid", latitude: 10.005, longitude: 106.0);

        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                PlaceStatus.Published, Arg.Any<CancellationToken>())
            .Returns([far, near, mid]);

        var sut = CreateSut();
        var result = await sut.GetNearbyAsync(origin.Lat, origin.Lng, radiusMeters: 5000, limit: null);

        result.Select(p => p.Name).Should().Equal("Near", "Mid", "Far");
        result.Select(p => p.DistanceMeters).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetNearbyAsync_ExcludesPlacesOutsideRequestedRadius()
    {
        var inRange = EntityFactory.CreatePlace(name: "InRange", latitude: 10.001, longitude: 106.0);
        var outOfRange = EntityFactory.CreatePlace(name: "OutOfRange", latitude: 10.1, longitude: 106.0);

        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                PlaceStatus.Published, Arg.Any<CancellationToken>())
            .Returns([inRange, outOfRange]);

        var sut = CreateSut();
        var result = await sut.GetNearbyAsync(10.0, 106.0, radiusMeters: 500, limit: null);

        result.Should().ContainSingle(p => p.Name == "InRange");
    }

    [Fact]
    public async Task GetNearbyAsync_RespectsLimit()
    {
        var places = Enumerable.Range(0, 5)
            .Select(i => EntityFactory.CreatePlace(name: $"P{i}", latitude: 10.0 + i * 0.0001, longitude: 106.0))
            .ToList();

        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                PlaceStatus.Published, Arg.Any<CancellationToken>())
            .Returns(places);

        var sut = CreateSut();
        var result = await sut.GetNearbyAsync(10.0, 106.0, radiusMeters: 5000, limit: 2);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetNearbyAsync_WhenRadiusZero_ReturnsNearestAmongAllPublished()
    {
        var far = EntityFactory.CreatePlace(name: "Far", latitude: 11.0, longitude: 106.0);
        var near = EntityFactory.CreatePlace(name: "Near", latitude: 10.01, longitude: 106.0);

        _placeRepository.GetAllPublishedAsync(Arg.Any<CancellationToken>()).Returns([far, near]);

        var sut = CreateSut();
        var result = await sut.GetNearbyAsync(10.0, 106.0, radiusMeters: 0, limit: 1);

        result.Should().ContainSingle(p => p.Name == "Near");
        await _placeRepository.DidNotReceive().GetInBoundingBoxAsync(
            Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
            Arg.Any<PlaceStatus?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_UsesDefaultsForInvalidPageAndPageSize()
    {
        _placeRepository
            .SearchAsync(
                null, null, null, null, null, false, 1, 20, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Place>)[], 0));

        var sut = CreateSut();
        var result = await sut.SearchAsync(new PlaceQueryRequest { Page = -1, PageSize = 0 });

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task SearchAsync_ParsesValidCategoryAndStatusFilters()
    {
        _placeRepository
            .SearchAsync(
                "temple", PlaceCategory.Religious, "Bến Thành", PlaceStatus.Published, "name", true, 1, 20,
                Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Place>)[], 0));

        var sut = CreateSut();
        await sut.SearchAsync(new PlaceQueryRequest
        {
            Search = "temple",
            Category = "Religious",
            Ward = "Bến Thành",
            Status = "Published",
            SortBy = "name",
            Descending = true,
        });

        await _placeRepository.Received(1).SearchAsync(
            "temple", PlaceCategory.Religious, "Bến Thành", PlaceStatus.Published, "name", true, 1, 20,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_IgnoresUnknownCategoryOrStatusInsteadOfThrowing()
    {
        _placeRepository
            .SearchAsync(
                Arg.Any<string?>(), null, Arg.Any<string?>(), null, Arg.Any<string?>(), false, 1, 20,
                Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Place>)[], 0));

        var sut = CreateSut();
        var result = await sut.SearchAsync(new PlaceQueryRequest { Category = "not-a-real-category", Status = "nope" });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithUnknownCategory_ThrowsValidationDomainException()
    {
        var sut = CreateSut();
        var request = new CreatePlaceRequest
        {
            Name = "Test",
            DescriptionVi = "Desc",
            Category = "NotACategory",
            Ward = "Bến Thành",
            Address = "123 St",
        };

        var act = async () => await sut.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationDomainException>();
    }

    [Fact]
    public async Task CreateAsync_SetsContentHashAndDefaultTriggerRadius_AndStartsAsDraft()
    {
        var sut = CreateSut();
        var request = new CreatePlaceRequest
        {
            Name = "  New Place  ",
            DescriptionVi = "  Some description  ",
            Category = "Museum",
            Ward = "Bến Thành",
            Address = "123 St",
            Latitude = 10,
            Longitude = 106,
        };

        var result = await sut.CreateAsync(request);

        result.Status.Should().Be(PlaceStatus.Draft.ToString());
        result.TriggerRadiusMeters.Should().Be(30);
        result.Description.Should().Be("Some description");

        await _placeRepository.Received(1).AddAsync(
            Arg.Is<Place>(p => p.ContentHash == TourismHcm.Domain.Helpers.ContentHash.Compute("Some description")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenTriggerRadiusProvided_UsesIt()
    {
        var sut = CreateSut();
        var request = new CreatePlaceRequest
        {
            Name = "New Place",
            DescriptionVi = "Desc",
            Category = "Park",
            Ward = "Bến Thành",
            Address = "123 St",
            TriggerRadiusMeters = 75,
        };

        var result = await sut.CreateAsync(request);

        result.TriggerRadiusMeters.Should().Be(75);
    }

    [Fact]
    public async Task UpdateAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdWithTranslationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Place?)null);

        var sut = CreateSut();
        var act = async () => await sut.UpdateAsync(Guid.NewGuid(), new UpdatePlaceRequest());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenDescriptionContentChanges_TriggersAudioRegeneration()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Original description.");
        _placeRepository.GetByIdWithTranslationsAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var sut = CreateSut();
        await sut.UpdateAsync(place.Id, new UpdatePlaceRequest { DescriptionVi = "Brand new description." });

        await _audioService.Received(1).RegenerateAllLanguagesAsync(place.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenDescriptionUnchanged_DoesNotTriggerAudioRegeneration()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Same description.");
        _placeRepository.GetByIdWithTranslationsAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var sut = CreateSut();

        // Re-submitting the exact same text (even with surrounding whitespace) must not be
        // treated as a content change: the computed hash is identical.
        await sut.UpdateAsync(place.Id, new UpdatePlaceRequest { DescriptionVi = "  Same description.  " });

        await _audioService.DidNotReceive().RegenerateAllLanguagesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenOnlyNameChanges_UpdatesNameWithoutTouchingDescription()
    {
        var place = EntityFactory.CreatePlace(name: "Old Name", descriptionVi: "Desc");
        _placeRepository.GetByIdWithTranslationsAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var sut = CreateSut();
        var result = await sut.UpdateAsync(place.Id, new UpdatePlaceRequest { Name = "New Name" });

        result.Name.Should().Be("New Name");
        await _audioService.DidNotReceive().RegenerateAllLanguagesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Place?)null);
        var sut = CreateSut();

        var act = async () => await sut.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenPlaceExists_RemovesIt()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var sut = CreateSut();
        await sut.DeleteAsync(place.Id);

        _placeRepository.Received(1).Remove(place);
    }
}

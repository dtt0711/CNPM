using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Tours;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class TourServiceTests
{
    private readonly ITourRepository _tourRepository = Substitute.For<ITourRepository>();
    private readonly IPlaceRepository _placeRepository = Substitute.For<IPlaceRepository>();
    private readonly IUnitOfWork _unitOfWork = FakeProviders.CreateUnitOfWork();

    private TourService CreateSut() => new(_tourRepository, _placeRepository, _unitOfWork);

    [Fact]
    public async Task CreateAsync_WhenPlaceIdsEmpty_ThrowsValidationDomainException()
    {
        var sut = CreateSut();
        var request = new CreateTourRequest { Name = "Empty Tour", PlaceIds = [] };

        var act = async () => await sut.CreateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationDomainException>();
    }

    [Fact]
    public async Task CreateAsync_WhenAPlaceDoesNotExist_ThrowsNotFound()
    {
        var existingPlace = EntityFactory.CreatePlace();
        var missingId = Guid.NewGuid();

        _placeRepository.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([existingPlace]);

        var sut = CreateSut();
        var request = new CreateTourRequest { Name = "Tour", PlaceIds = [existingPlace.Id, missingId] };

        var act = async () => await sut.CreateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_WithExplicitStartCoordinates_OrdersStopsByNearestNeighborFromStart()
    {
        // Three places laid out along the same latitude line at x = 0, 10, 20 (in degree-ish
        // units); starting near x = 11 should visit the x=10 place first, then x=20, then x=0.
        var placeNear10 = EntityFactory.CreatePlace(name: "Near10", latitude: 0, longitude: 10.0001);
        var placeAt0 = EntityFactory.CreatePlace(name: "At0", latitude: 0, longitude: 0);
        var placeAt20 = EntityFactory.CreatePlace(name: "At20", latitude: 0, longitude: 20);

        var places = new List<Place> { placeAt0, placeNear10, placeAt20 };
        _placeRepository.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(places);

        var sut = CreateSut();
        var request = new CreateTourRequest
        {
            Name = "Nearest Neighbor Tour",
            PlaceIds = [placeAt0.Id, placeNear10.Id, placeAt20.Id],
            StartLatitude = 0,
            StartLongitude = 10,
        };

        var result = await sut.CreateAsync(Guid.NewGuid(), request);

        result.Stops.Select(s => s.PlaceName).Should().Equal("Near10", "At20", "At0");
        result.Stops.Select(s => s.Order).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task CreateAsync_WithoutStartCoordinates_StartsFromFirstRequestedPlaceId()
    {
        var placeA = EntityFactory.CreatePlace(name: "A", latitude: 0, longitude: 0);
        var placeB = EntityFactory.CreatePlace(name: "B", latitude: 0, longitude: 1);
        var placeC = EntityFactory.CreatePlace(name: "C", latitude: 0, longitude: 2);

        var places = new List<Place> { placeC, placeB, placeA };
        _placeRepository.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(places);

        var sut = CreateSut();
        // Request order explicitly starts with C, even though C is geographically furthest.
        var request = new CreateTourRequest { Name = "Tour", PlaceIds = [placeC.Id, placeA.Id, placeB.Id] };

        var result = await sut.CreateAsync(Guid.NewGuid(), request);

        result.Stops.Select(s => s.PlaceName).Should().Equal("C", "B", "A");
    }

    [Fact]
    public async Task CreateAsync_PersistsTour_WithRequestingUserId()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns([place]);

        var userId = Guid.NewGuid();
        var sut = CreateSut();
        var result = await sut.CreateAsync(userId, new CreateTourRequest { Name = "Solo Tour", PlaceIds = [place.Id] });

        result.UserId.Should().Be(userId);
        result.Name.Should().Be("Solo Tour");
        await _tourRepository.Received(1).AddAsync(Arg.Is<Tour>(t => t.UserId == userId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenTourMissing_ThrowsNotFound()
    {
        _tourRepository.GetByIdWithStopsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Tour?)null);
        var sut = CreateSut();

        var act = async () => await sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotOwner_ThrowsForbidden()
    {
        var tour = EntityFactory.CreateTour(Guid.NewGuid());
        _tourRepository.GetByIdWithStopsAsync(tour.Id, Arg.Any<CancellationToken>()).Returns(tour);

        var sut = CreateSut();
        var act = async () => await sut.GetByIdAsync(Guid.NewGuid(), tour.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenOwner_ReturnsTour()
    {
        var userId = Guid.NewGuid();
        var tour = EntityFactory.CreateTour(userId);
        _tourRepository.GetByIdWithStopsAsync(tour.Id, Arg.Any<CancellationToken>()).Returns(tour);

        var sut = CreateSut();
        var result = await sut.GetByIdAsync(userId, tour.Id);

        result.Id.Should().Be(tour.Id);
    }

    [Fact]
    public async Task GetByUserAsync_ReturnsAllToursForUser()
    {
        var userId = Guid.NewGuid();
        var tours = new List<Tour> { EntityFactory.CreateTour(userId), EntityFactory.CreateTour(userId) };
        _tourRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(tours);

        var sut = CreateSut();
        var result = await sut.GetByUserAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotOwner_ThrowsForbidden()
    {
        var tour = EntityFactory.CreateTour(Guid.NewGuid());
        _tourRepository.GetByIdAsync(tour.Id, Arg.Any<CancellationToken>()).Returns(tour);

        var sut = CreateSut();
        var act = async () => await sut.DeleteAsync(Guid.NewGuid(), tour.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenOwner_RemovesTour()
    {
        var userId = Guid.NewGuid();
        var tour = EntityFactory.CreateTour(userId);
        _tourRepository.GetByIdAsync(tour.Id, Arg.Any<CancellationToken>()).Returns(tour);

        var sut = CreateSut();
        await sut.DeleteAsync(userId, tour.Id);

        _tourRepository.Received(1).Remove(tour);
    }
}

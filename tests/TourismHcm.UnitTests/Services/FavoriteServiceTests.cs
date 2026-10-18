using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Services;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class FavoriteServiceTests
{
    private readonly IFavoriteRepository _favoriteRepository = Substitute.For<IFavoriteRepository>();
    private readonly IPlaceRepository _placeRepository = Substitute.For<IPlaceRepository>();
    private readonly IUnitOfWork _unitOfWork = FakeProviders.CreateUnitOfWork();

    private FavoriteService CreateSut() => new(_favoriteRepository, _placeRepository, _unitOfWork);

    [Fact]
    public async Task GetByUserAsync_ReturnsMappedFavorites()
    {
        var userId = Guid.NewGuid();
        var place = EntityFactory.CreatePlace(name: "Favorite Place");
        var favorite = new Favorite { UserId = userId, PlaceId = place.Id, CreatedAt = DateTimeOffset.UtcNow, Place = place };
        _favoriteRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns([favorite]);

        var sut = CreateSut();
        var result = await sut.GetByUserAsync(userId);

        result.Should().ContainSingle(f => f.PlaceId == place.Id && f.PlaceName == "Favorite Place");
    }

    [Fact]
    public async Task AddAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Place?)null);
        var sut = CreateSut();

        var act = async () => await sut.AddAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddAsync_WhenAlreadyFavorited_IsIdempotent()
    {
        var place = EntityFactory.CreatePlace();
        var userId = Guid.NewGuid();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _favoriteRepository.GetAsync(userId, place.Id, Arg.Any<CancellationToken>())
            .Returns(new Favorite { UserId = userId, PlaceId = place.Id });

        var sut = CreateSut();
        await sut.AddAsync(userId, place.Id);

        await _favoriteRepository.DidNotReceive().AddAsync(Arg.Any<Favorite>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_WhenNotYetFavorited_AddsIt()
    {
        var place = EntityFactory.CreatePlace();
        var userId = Guid.NewGuid();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _favoriteRepository.GetAsync(userId, place.Id, Arg.Any<CancellationToken>()).Returns((Favorite?)null);

        var sut = CreateSut();
        await sut.AddAsync(userId, place.Id);

        await _favoriteRepository.Received(1).AddAsync(
            Arg.Is<Favorite>(f => f.UserId == userId && f.PlaceId == place.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_WhenNotFavorited_IsNoOp()
    {
        _favoriteRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Favorite?)null);

        var sut = CreateSut();
        await sut.RemoveAsync(Guid.NewGuid(), Guid.NewGuid());

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_WhenFavorited_RemovesIt()
    {
        var userId = Guid.NewGuid();
        var placeId = Guid.NewGuid();
        var favorite = new Favorite { UserId = userId, PlaceId = placeId };
        _favoriteRepository.GetAsync(userId, placeId, Arg.Any<CancellationToken>()).Returns(favorite);

        var sut = CreateSut();
        await sut.RemoveAsync(userId, placeId);

        _favoriteRepository.Received(1).Remove(favorite);
    }
}

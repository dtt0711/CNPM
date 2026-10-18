using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Reviews;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class ReviewServiceTests
{
    private readonly IReviewRepository _reviewRepository = Substitute.For<IReviewRepository>();
    private readonly IPlaceRepository _placeRepository = Substitute.For<IPlaceRepository>();
    private readonly IUnitOfWork _unitOfWork = FakeProviders.CreateUnitOfWork();

    private ReviewService CreateSut() => new(_reviewRepository, _placeRepository, _unitOfWork);

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task CreateOrUpdateAsync_WhenRatingOutOfRange_ThrowsValidationDomainException(int rating)
    {
        var sut = CreateSut();
        var request = new CreateReviewRequest { PlaceId = Guid.NewGuid(), Rating = rating };

        var act = async () => await sut.CreateOrUpdateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationDomainException>();
    }

    [Fact]
    public async Task CreateOrUpdateAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Place?)null);
        var sut = CreateSut();
        var request = new CreateReviewRequest { PlaceId = Guid.NewGuid(), Rating = 5 };

        var act = async () => await sut.CreateOrUpdateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateOrUpdateAsync_WhenNoExistingReview_CreatesNewReview_AndRecalculatesPlaceStats()
    {
        var place = EntityFactory.CreatePlace();
        var userId = Guid.NewGuid();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _reviewRepository.GetByUserAndPlaceAsync(userId, place.Id, Arg.Any<CancellationToken>())
            .Returns((Review?)null);
        _reviewRepository.GetStatsForPlaceAsync(place.Id, Arg.Any<CancellationToken>())
            .Returns((4.5, 2));

        var sut = CreateSut();
        var request = new CreateReviewRequest { PlaceId = place.Id, Rating = 4, Comment = "Nice" };

        var result = await sut.CreateOrUpdateAsync(userId, request);

        result.Rating.Should().Be(4);
        result.Comment.Should().Be("Nice");

        await _reviewRepository.Received(1).AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
        place.AverageRating.Should().Be(4.5);
        place.ReviewCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_WhenReviewAlreadyExistsForUserAndPlace_UpdatesInPlace()
    {
        var place = EntityFactory.CreatePlace();
        var userId = Guid.NewGuid();
        var existingReview = EntityFactory.CreateReview(place.Id, userId, rating: 2, comment: "Meh");

        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _reviewRepository.GetByUserAndPlaceAsync(userId, place.Id, Arg.Any<CancellationToken>())
            .Returns(existingReview);
        _reviewRepository.GetStatsForPlaceAsync(place.Id, Arg.Any<CancellationToken>())
            .Returns((5.0, 1));

        var sut = CreateSut();
        var request = new CreateReviewRequest { PlaceId = place.Id, Rating = 5, Comment = "Actually great" };

        var result = await sut.CreateOrUpdateAsync(userId, request);

        result.Id.Should().Be(existingReview.Id);
        result.Rating.Should().Be(5);
        result.Comment.Should().Be("Actually great");

        await _reviewRepository.DidNotReceive().AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1.0, 2.5, 3)]
    [InlineData(4.0, 4.333, 3)]
    [InlineData(0.0, 0.0, 0)]
    public async Task CreateOrUpdateAsync_RecalculatesPlaceAverageRating_RoundedToTwoDecimals(
        double _, double average, int count)
    {
        var place = EntityFactory.CreatePlace();
        var userId = Guid.NewGuid();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _reviewRepository.GetByUserAndPlaceAsync(userId, place.Id, Arg.Any<CancellationToken>())
            .Returns((Review?)null);
        _reviewRepository.GetStatsForPlaceAsync(place.Id, Arg.Any<CancellationToken>())
            .Returns((average, count));

        var sut = CreateSut();
        await sut.CreateOrUpdateAsync(userId, new CreateReviewRequest { PlaceId = place.Id, Rating = 4 });

        place.AverageRating.Should().Be(Math.Round(average, 2));
        place.ReviewCount.Should().Be(count);
    }

    [Fact]
    public async Task GetByPlaceAsync_WhenPageOrPageSizeInvalid_FallsBackToDefaults()
    {
        var placeId = Guid.NewGuid();
        _reviewRepository.GetByPlaceIdAsync(placeId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Review>)[], 0));

        var sut = CreateSut();
        var result = await sut.GetByPlaceAsync(placeId, page: 0, pageSize: 1000);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetByPlaceAsync_ReturnsPagedReviews()
    {
        var place = EntityFactory.CreatePlace();
        var review = EntityFactory.CreateReview(place.Id, Guid.NewGuid());
        _reviewRepository.GetByPlaceIdAsync(place.Id, 1, 20, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Review>)[review], 1));

        var sut = CreateSut();
        var result = await sut.GetByPlaceAsync(place.Id, 1, 20);

        result.Items.Should().ContainSingle(r => r.Id == review.Id);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenReviewMissing_ThrowsNotFound()
    {
        _reviewRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Review?)null);
        var sut = CreateSut();

        var act = async () => await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), isAdmin: false);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotOwnerAndNotAdmin_ThrowsForbidden()
    {
        var place = EntityFactory.CreatePlace();
        var review = EntityFactory.CreateReview(place.Id, Guid.NewGuid());
        _reviewRepository.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var sut = CreateSut();
        var act = async () => await sut.DeleteAsync(Guid.NewGuid(), review.Id, isAdmin: false);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _reviewRepository.DidNotReceive().AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenOwner_RemovesReview_AndRecalculatesStats()
    {
        var place = EntityFactory.CreatePlace();
        var userId = Guid.NewGuid();
        var review = EntityFactory.CreateReview(place.Id, userId);
        _reviewRepository.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _reviewRepository.GetStatsForPlaceAsync(place.Id, Arg.Any<CancellationToken>()).Returns((0.0, 0));

        var sut = CreateSut();
        await sut.DeleteAsync(userId, review.Id, isAdmin: false);

        _reviewRepository.Received(1).Remove(review);
        place.ReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_WhenAdmin_CanRemoveOtherUsersReview()
    {
        var place = EntityFactory.CreatePlace();
        var review = EntityFactory.CreateReview(place.Id, Guid.NewGuid());
        _reviewRepository.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _reviewRepository.GetStatsForPlaceAsync(place.Id, Arg.Any<CancellationToken>()).Returns((0.0, 0));

        var sut = CreateSut();
        await sut.DeleteAsync(Guid.NewGuid(), review.Id, isAdmin: true);

        _reviewRepository.Received(1).Remove(review);
    }
}

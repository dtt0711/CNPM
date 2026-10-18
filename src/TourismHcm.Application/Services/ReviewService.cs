using Mapster;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Reviews;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Application.Services;

public sealed class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IPlaceRepository _placeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewService(
        IReviewRepository reviewRepository,
        IPlaceRepository placeRepository,
        IUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository;
        _placeRepository = placeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReviewDto> CreateOrUpdateAsync(Guid userId, CreateReviewRequest request, CancellationToken ct = default)
    {
        if (request.Rating is < 1 or > 5)
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                [nameof(request.Rating)] = ["Rating must be between 1 and 5."]
            });
        }

        var place = await _placeRepository.GetByIdAsync(request.PlaceId, ct)
            ?? throw new NotFoundException("Place", request.PlaceId);

        var now = DateTimeOffset.UtcNow;
        var existing = await _reviewRepository.GetByUserAndPlaceAsync(userId, request.PlaceId, ct);

        Review review;
        if (existing is not null)
        {
            existing.Rating = request.Rating;
            existing.Comment = request.Comment;
            existing.UpdatedAt = now;
            review = existing;
        }
        else
        {
            review = new Review
            {
                Id = Guid.NewGuid(),
                PlaceId = request.PlaceId,
                UserId = userId,
                Rating = request.Rating,
                Comment = request.Comment,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _reviewRepository.AddAsync(review, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await RecalculatePlaceStatsAsync(place, ct);

        return review.Adapt<ReviewDto>();
    }

    public async Task<PagedResult<ReviewDto>> GetByPlaceAsync(Guid placeId, int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = page <= 0 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var (items, totalCount) = await _reviewRepository.GetByPlaceIdAsync(placeId, safePage, safePageSize, ct);
        var dtos = items.Adapt<List<ReviewDto>>();

        return PagedResult<ReviewDto>.Create(dtos, safePage, safePageSize, totalCount);
    }

    public async Task DeleteAsync(Guid userId, Guid reviewId, bool isAdmin, CancellationToken ct = default)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId, ct)
            ?? throw new NotFoundException("Review", reviewId);

        if (!isAdmin && review.UserId != userId)
        {
            throw new ForbiddenException("You can only delete your own review.");
        }

        var place = await _placeRepository.GetByIdAsync(review.PlaceId, ct);

        _reviewRepository.Remove(review);
        await _unitOfWork.SaveChangesAsync(ct);

        if (place is not null)
        {
            await RecalculatePlaceStatsAsync(place, ct);
        }
    }

    private async Task RecalculatePlaceStatsAsync(Place place, CancellationToken ct)
    {
        var (average, count) = await _reviewRepository.GetStatsForPlaceAsync(place.Id, ct);
        place.AverageRating = Math.Round(average, 2);
        place.ReviewCount = count;
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

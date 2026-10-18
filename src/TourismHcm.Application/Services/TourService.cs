using Mapster;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Tours;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.Application.Services;

public sealed class TourService : ITourService
{
    private readonly ITourRepository _tourRepository;
    private readonly IPlaceRepository _placeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TourService(
        ITourRepository tourRepository,
        IPlaceRepository placeRepository,
        IUnitOfWork unitOfWork)
    {
        _tourRepository = tourRepository;
        _placeRepository = placeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TourDto> CreateAsync(Guid userId, CreateTourRequest request, CancellationToken ct = default)
    {
        var distinctIds = request.PlaceIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                [nameof(request.PlaceIds)] = ["At least one place is required to create a tour."]
            });
        }

        var places = await _placeRepository.GetByIdsAsync(distinctIds, ct);
        if (places.Count != distinctIds.Count)
        {
            var foundIds = places.Select(p => p.Id).ToHashSet();
            var missing = distinctIds.Where(id => !foundIds.Contains(id));
            throw new NotFoundException("Place", string.Join(",", missing));
        }

        var ordered = OrderByNearestNeighbor(places, request);

        var now = DateTimeOffset.UtcNow;
        var tour = new Tour
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        for (var i = 0; i < ordered.Count; i++)
        {
            tour.Stops.Add(new TourStop
            {
                Id = Guid.NewGuid(),
                TourId = tour.Id,
                PlaceId = ordered[i].Id,
                Order = i,
                Place = ordered[i]
            });
        }

        await _tourRepository.AddAsync(tour, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return tour.Adapt<TourDto>();
    }

    public async Task<TourDto> GetByIdAsync(Guid userId, Guid tourId, CancellationToken ct = default)
    {
        var tour = await _tourRepository.GetByIdWithStopsAsync(tourId, ct)
            ?? throw new NotFoundException("Tour", tourId);

        if (tour.UserId != userId)
        {
            throw new ForbiddenException("You do not have access to this tour.");
        }

        return tour.Adapt<TourDto>();
    }

    public async Task<IReadOnlyList<TourDto>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tours = await _tourRepository.GetByUserIdAsync(userId, ct);
        return tours.Adapt<List<TourDto>>();
    }

    public async Task DeleteAsync(Guid userId, Guid tourId, CancellationToken ct = default)
    {
        var tour = await _tourRepository.GetByIdAsync(tourId, ct)
            ?? throw new NotFoundException("Tour", tourId);

        if (tour.UserId != userId)
        {
            throw new ForbiddenException("You do not have access to this tour.");
        }

        _tourRepository.Remove(tour);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static List<Place> OrderByNearestNeighbor(IReadOnlyList<Place> places, CreateTourRequest request)
    {
        var remaining = new List<Place>(places);
        var ordered = new List<Place>(places.Count);

        double currentLat;
        double currentLng;

        if (request.StartLatitude.HasValue && request.StartLongitude.HasValue)
        {
            currentLat = request.StartLatitude.Value;
            currentLng = request.StartLongitude.Value;
        }
        else
        {
            var firstId = request.PlaceIds[0];
            var first = remaining.First(p => p.Id == firstId);
            ordered.Add(first);
            remaining.Remove(first);
            currentLat = first.Latitude;
            currentLng = first.Longitude;
        }

        while (remaining.Count > 0)
        {
            var nearest = remaining
                .OrderBy(p => GeoHelper.HaversineMeters(currentLat, currentLng, p.Latitude, p.Longitude))
                .First();

            ordered.Add(nearest);
            remaining.Remove(nearest);
            currentLat = nearest.Latitude;
            currentLng = nearest.Longitude;
        }

        return ordered;
    }
}

using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.UnitTests.Helpers;

/// <summary>
/// Builds Domain entities with consistent defaults so service tests can focus on behaviour.
/// </summary>
public static class FixtureHelpers
{
    public static Place CreatePlace(
        string? name = null,
        double latitude = 10.7769,
        double longitude = 106.6959,
        int triggerRadiusMeters = 30,
        PlaceStatus status = PlaceStatus.Published,
        string descriptionVi = "Mô tả mẫu bằng tiếng Việt.",
        Guid? id = null,
        PlaceCategory category = PlaceCategory.Landmark,
        string district = "Quận 1")
    {
        return new Place
        {
            Id = id ?? Guid.NewGuid(),
            Name = name ?? "Sample Place",
            DescriptionVi = descriptionVi,
            ContentHash = ContentHash.Compute(descriptionVi),
            Category = category,
            District = district,
            Address = "123 Sample Street",
            Latitude = latitude,
            Longitude = longitude,
            TriggerRadiusMeters = triggerRadiusMeters,
            OpeningHours = "08:00-17:00",
            TicketPrice = "Free",
            ImageUrl = "https://example.com/image.jpg",
            Tags = "sample,tag",
            AverageRating = 0,
            ReviewCount = 0,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static User CreateUser(string email = "user@example.com", bool isActive = true, string? displayName = null)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName ?? "Test User",
            PasswordHash = "hashed-password",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static Role CreateRole(string name) => new() { Id = Guid.NewGuid(), Name = name };

    public static RefreshToken CreateRefreshToken(
        Guid userId, string tokenHash, bool revoked = false, DateTimeOffset? expiresAt = null)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            RevokedAt = revoked ? DateTimeOffset.UtcNow : null,
        };
    }

    public static Review CreateReview(Guid placeId, Guid userId, int rating = 5, string? comment = "Great!")
    {
        return new Review
        {
            Id = Guid.NewGuid(),
            PlaceId = placeId,
            UserId = userId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static Tour CreateTour(Guid userId, string name = "My Tour")
    {
        return new Tour
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static AudioAsset CreateAudioAsset(
        Guid placeId, string lang, AudioAssetStatus status = AudioAssetStatus.Ready, string contentHash = "hash")
    {
        return new AudioAsset
        {
            Id = Guid.NewGuid(),
            PlaceId = placeId,
            Lang = lang,
            ContentHash = contentHash,
            Path = $"{placeId}/{lang}.mp3",
            DurationSec = 12.3,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static AudioGenerationTask CreateAudioTask(
        Guid placeId, string lang, AudioTaskStatus status = AudioTaskStatus.Pending, string contentHash = "hash")
    {
        return new AudioGenerationTask
        {
            Id = Guid.NewGuid(),
            PlaceId = placeId,
            Lang = lang,
            ContentHash = contentHash,
            Status = status,
            Attempts = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static PlaceTranslation CreateTranslation(
        Guid placeId,
        string lang,
        string text = "Translated text",
        TranslationSource source = TranslationSource.Machine,
        string contentHash = "hash")
    {
        return new PlaceTranslation
        {
            Id = Guid.NewGuid(),
            PlaceId = placeId,
            Lang = lang,
            Text = text,
            Source = source,
            ContentHash = contentHash,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}

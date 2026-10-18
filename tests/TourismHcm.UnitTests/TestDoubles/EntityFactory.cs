using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.UnitTests.Helpers;

namespace TourismHcm.UnitTests.TestDoubles;

/// <summary>
/// Back-compat aliases over <see cref="FixtureHelpers"/>. Existing service tests keep calling
/// <c>EntityFactory</c>; new tests can use either name.
/// </summary>
public static class EntityFactory
{
    public static Place CreatePlace(
        string? name = null,
        double latitude = 10.7769,
        double longitude = 106.6959,
        int triggerRadiusMeters = 30,
        PlaceStatus status = PlaceStatus.Published,
        string descriptionVi = "Mô tả mẫu bằng tiếng Việt.",
        Guid? id = null) =>
        FixtureHelpers.CreatePlace(name, latitude, longitude, triggerRadiusMeters, status, descriptionVi, id);

    public static User CreateUser(string email = "user@example.com", bool isActive = true, string? displayName = null) =>
        FixtureHelpers.CreateUser(email, isActive, displayName);

    public static Role CreateRole(string name) => FixtureHelpers.CreateRole(name);

    public static RefreshToken CreateRefreshToken(
        Guid userId, string tokenHash, bool revoked = false, DateTimeOffset? expiresAt = null) =>
        FixtureHelpers.CreateRefreshToken(userId, tokenHash, revoked, expiresAt);

    public static Review CreateReview(Guid placeId, Guid userId, int rating = 5, string? comment = "Great!") =>
        FixtureHelpers.CreateReview(placeId, userId, rating, comment);

    public static Tour CreateTour(Guid userId, string name = "My Tour") =>
        FixtureHelpers.CreateTour(userId, name);

    public static AudioAsset CreateAudioAsset(
        Guid placeId, string lang, AudioAssetStatus status = AudioAssetStatus.Ready, string contentHash = "hash") =>
        FixtureHelpers.CreateAudioAsset(placeId, lang, status, contentHash);

    public static AudioGenerationTask CreateAudioTask(
        Guid placeId, string lang, AudioTaskStatus status = AudioTaskStatus.Pending, string contentHash = "hash") =>
        FixtureHelpers.CreateAudioTask(placeId, lang, status, contentHash);

    public static PlaceTranslation CreateTranslation(
        Guid placeId,
        string lang,
        string text = "Translated text",
        TranslationSource source = TranslationSource.Machine,
        string contentHash = "hash") =>
        FixtureHelpers.CreateTranslation(placeId, lang, text, source, contentHash);
}

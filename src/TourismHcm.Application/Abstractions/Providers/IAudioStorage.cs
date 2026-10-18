namespace TourismHcm.Application.Abstractions.Providers;

public interface IAudioStorage
{
    Task<string> SaveAsync(Guid placeId, string lang, string contentHash, byte[] data, CancellationToken ct = default);

    Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default);

    Task<bool> ExistsAsync(string path, CancellationToken ct = default);

    string GetPublicUrl(string path);
}

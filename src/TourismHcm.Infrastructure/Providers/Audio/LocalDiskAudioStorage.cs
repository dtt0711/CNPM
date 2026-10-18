using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Options;

namespace TourismHcm.Infrastructure.Providers.Audio;

/// <summary>
/// Stores generated narration audio files on local disk under
/// <c>{RootPath}/{placeId}/{lang}/{contentHash}.mp3</c> and serves them back through a
/// configurable public base URL (e.g. a static files mount in the API).
/// </summary>
public sealed class LocalDiskAudioStorage : IAudioStorage
{
    private readonly AudioStorageOptions _options;

    public LocalDiskAudioStorage(IOptions<AudioStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> SaveAsync(Guid placeId, string lang, string contentHash, byte[] data, CancellationToken ct = default)
    {
        var relativePath = BuildRelativePath(placeId, lang, contentHash);
        var fullPath = ToFullPath(relativePath);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, data, ct);

        return relativePath;
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var fullPath = ToFullPath(path);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(File.Exists(ToFullPath(path)));

    public string GetPublicUrl(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{normalized}";
    }

    private static string BuildRelativePath(Guid placeId, string lang, string contentHash) =>
        Path.Combine(placeId.ToString(), lang, $"{contentHash}.mp3").Replace('\\', '/');

    private string ToFullPath(string relativePath)
    {
        var root = Path.IsPathRooted(_options.RootPath)
            ? _options.RootPath
            : Path.Combine(AppContext.BaseDirectory, _options.RootPath);

        var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(root, normalizedRelative);
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Options;
using TourismHcm.Contracts.Maps;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Application.Services;

public sealed class MapPackService : IMapPackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly MapPackOptions _options;

    public MapPackService(IOptions<MapPackOptions> options)
    {
        _options = options.Value;
    }

    public Task<MapOfflineOptionsDto> GetOfflineOptionsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var packsDir = GetPacksDirectory();
        var packs = Directory.Exists(packsDir)
            ? Directory.GetDirectories(packsDir).Select(Path.GetFileName).Where(n => !string.IsNullOrWhiteSpace(n)).Cast<string>().ToArray()
            : Array.Empty<string>();

        return Task.FromResult(new MapOfflineOptionsDto
        {
            DefaultScope = _options.DefaultScope,
            AvailablePacks = packs
        });
    }

    public async Task<MapPackManifestDto> GetOfflineManifestAsync(string packId, CancellationToken ct = default)
    {
        var safePackId = SanitizeSegment(packId);
        var packDir = ResolveSafePath(Path.Combine("packs", safePackId));
        if (!Directory.Exists(packDir))
        {
            return new MapPackManifestDto
            {
                PackId = safePackId,
                Scope = _options.DefaultScope,
                Version = "1",
                Files = Array.Empty<MapPackFileDto>()
            };
        }

        var files = new Dictionary<string, MapPackFileDto>(StringComparer.OrdinalIgnoreCase);
        var scope = _options.DefaultScope;
        var version = "1";

        var manifestPath = Path.Combine(packDir, "manifest.json");
        if (File.Exists(manifestPath))
        {
            await using var stream = File.OpenRead(manifestPath);
            var disk = await JsonSerializer.DeserializeAsync<DiskManifest>(stream, JsonOptions, ct);
            if (disk is not null)
            {
                if (!string.IsNullOrWhiteSpace(disk.Scope))
                {
                    scope = disk.Scope;
                }

                if (!string.IsNullOrWhiteSpace(disk.Version))
                {
                    version = disk.Version;
                }

                foreach (var item in disk.AllFiles())
                {
                    if (string.IsNullOrWhiteSpace(item.Path))
                    {
                        continue;
                    }

                    files[item.Path] = item.ToDto();
                }
            }
        }

        foreach (var pmtiles in Directory.GetFiles(packDir, "*.pmtiles"))
        {
            var name = Path.GetFileName(pmtiles);
            files.TryGetValue(name, out var existing);
            var info = new FileInfo(pmtiles);
            files[name] = new MapPackFileDto
            {
                Path = name,
                SizeBytes = info.Length,
                Sha256 = string.IsNullOrWhiteSpace(existing?.Sha256) || existing.Sha256.Contains("HASH", StringComparison.OrdinalIgnoreCase)
                    ? TryComputeSha256(pmtiles)
                    : existing.Sha256
            };
        }

        return new MapPackManifestDto
        {
            PackId = safePackId,
            Scope = scope,
            Version = version,
            Files = files.Values.ToArray()
        };
    }

    public async Task<IReadOnlyList<MapPackManifestDto>> ListPacksAsync(CancellationToken ct = default)
    {
        var options = await GetOfflineOptionsAsync(ct);
        var manifests = new List<MapPackManifestDto>(options.AvailablePacks.Count);
        foreach (var packId in options.AvailablePacks)
        {
            manifests.Add(await GetOfflineManifestAsync(packId, ct));
        }

        return manifests;
    }

    private string GetRootDirectory()
    {
        return Path.IsPathRooted(_options.DataDir)
            ? _options.DataDir
            : Path.Combine(AppContext.BaseDirectory, _options.DataDir);
    }

    private string GetPacksDirectory() => Path.Combine(GetRootDirectory(), "packs");

    private string ResolveSafePath(string relativePath)
    {
        var root = Path.GetFullPath(GetRootDirectory());
        var combined = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Invalid map pack path.");
        }

        return combined;
    }

    private static string SanitizeSegment(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Contains("..", StringComparison.Ordinal) || trimmed.Contains(Path.DirectorySeparatorChar)
            || trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                ["packId"] = ["Invalid pack identifier."]
            });
        }

        return trimmed;
    }

    internal static string? TryComputeSha256(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return null;
        }

        using var stream = File.OpenRead(fullPath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class DiskManifest
    {
        public string? PackId { get; set; }
        public string? Id { get; set; }
        public string? Scope { get; set; }
        public string? Version { get; set; }
        public List<DiskFile>? Files { get; set; }
        public List<DiskFile>? Assets { get; set; }

        public IEnumerable<DiskFile> AllFiles()
        {
            if (Files is { Count: > 0 })
            {
                foreach (var f in Files)
                {
                    yield return f;
                }
            }

            if (Assets is { Count: > 0 })
            {
                foreach (var f in Assets)
                {
                    yield return f;
                }
            }
        }
    }

    private sealed class DiskFile
    {
        public string Path { get; set; } = string.Empty;
        public string? Sha256 { get; set; }
        public long? SizeBytes { get; set; }
        public long? Bytes { get; set; }

        public MapPackFileDto ToDto() => new()
        {
            Path = Path,
            Sha256 = Sha256,
            SizeBytes = SizeBytes ?? Bytes
        };
    }
}

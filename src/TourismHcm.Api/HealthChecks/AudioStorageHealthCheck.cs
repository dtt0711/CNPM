using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Options;

namespace TourismHcm.Api.HealthChecks;

/// <summary>
/// Verifies the configured audio storage root directory exists and is writable, by creating
/// and deleting a small temporary probe file.
/// </summary>
public sealed class AudioStorageHealthCheck : IHealthCheck
{
    private readonly AudioStorageOptions _options;

    public AudioStorageHealthCheck(IOptions<AudioStorageOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = Path.IsPathRooted(_options.RootPath)
                ? _options.RootPath
                : Path.Combine(AppContext.BaseDirectory, _options.RootPath);

            Directory.CreateDirectory(root);

            var probePath = Path.Combine(root, $".healthcheck-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);

            return Task.FromResult(HealthCheckResult.Healthy($"Audio storage directory '{root}' is writable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Audio storage directory is not writable.", ex));
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Options;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Providers.Security;

/// <summary>
/// Clears encrypted national IDs after <see cref="PiiOptions.RedactionDays"/> (default 180).
/// </summary>
public sealed class PiiRedactionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PiiOptions _options;
    private readonly ILogger<PiiRedactionBackgroundService> _logger;

    public PiiRedactionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<PiiOptions> options,
        ILogger<PiiRedactionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay first pass so the API can finish bootstrapping.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RedactAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "PII redaction pass failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RedactAsync(CancellationToken ct)
    {
        var days = _options.RedactionDays <= 0 ? 180 : _options.RedactionDays;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TourismHcmDbContext>();

        var users = await db.Users
            .Where(u =>
                u.PiiRedactedAt == null
                && u.NationalIdEncrypted != null
                && u.NationalIdEncrypted != ""
                && u.CreatedAt <= cutoff)
            .ToListAsync(ct);

        if (users.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var user in users)
        {
            user.NationalIdEncrypted = null;
            user.PiiRedactedAt = now;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("PII redacted for {Count} users older than {Days} days", users.Count, days);
    }
}

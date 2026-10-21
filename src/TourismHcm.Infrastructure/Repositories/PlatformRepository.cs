using Microsoft.EntityFrameworkCore;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Domain.Entities;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class PlatformRepository : IPlatformRepository
{
    private readonly TourismHcmDbContext _dbContext;

    public PlatformRepository(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> ListUsersAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = _dbContext.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<User?> GetUserWithRolesAsync(Guid userId, CancellationToken ct = default) =>
        _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

    public async Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is not null)
        {
            user.IsActive = isActive;
        }
    }

    public async Task AssignUserRolesAsync(Guid userId, IReadOnlyList<string> roleNames, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return;
        }

        var normalized = roleNames.Select(r => r.Trim().ToLowerInvariant()).Distinct().ToArray();
        var roles = await _dbContext.Roles.Where(r => normalized.Contains(r.Name)).ToListAsync(ct);

        user.UserRoles.Clear();
        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
        }
    }

    public Task<IReadOnlyList<Role>> GetAllRolesAsync(CancellationToken ct = default) =>
        _dbContext.Roles.AsNoTracking().OrderBy(r => r.Priority).ThenBy(r => r.Name).ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Role>)t.Result, ct);

    public Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default) =>
        _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);

    public async Task UpdateRolePermissionsAsync(Guid roleId, string permissionsJson, CancellationToken ct = default)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is not null)
        {
            role.PermissionsJson = permissionsJson;
        }
    }

    public Task<AiUsageLimit?> GetAiUsageForDateAsync(Guid userId, DateTime dateUtc, CancellationToken ct = default) =>
        _dbContext.AiUsageLimits.FirstOrDefaultAsync(
            u => u.UserId == userId && u.DateUtc == dateUtc.Date, ct);

    public async Task IncrementAiUsageAsync(Guid userId, DateTime dateUtc, CancellationToken ct = default)
    {
        var day = dateUtc.Date;
        var existing = await _dbContext.AiUsageLimits
            .FirstOrDefaultAsync(u => u.UserId == userId && u.DateUtc == day, ct);

        if (existing is null)
        {
            await _dbContext.AiUsageLimits.AddAsync(new AiUsageLimit
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DateUtc = day,
                Count = 1
            }, ct);
        }
        else
        {
            existing.Count += 1;
        }
    }

    public async Task<AnalyticsDevice> GetOrCreateAnalyticsDeviceAsync(string deviceKeyHash, CancellationToken ct = default)
    {
        var existing = await _dbContext.AnalyticsDevices
            .FirstOrDefaultAsync(d => d.DeviceKeyHash == deviceKeyHash, ct);

        if (existing is not null)
        {
            existing.LastSeenAt = DateTimeOffset.UtcNow;
            return existing;
        }

        var device = new AnalyticsDevice
        {
            Id = Guid.NewGuid(),
            DeviceKeyHash = deviceKeyHash,
            ConsentAnalytics = true,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        };

        await _dbContext.AnalyticsDevices.AddAsync(device, ct);
        return device;
    }

    public Task<AnalyticsSession?> GetAnalyticsSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        _dbContext.AnalyticsSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);

    public async Task AddAnalyticsSessionAsync(AnalyticsSession session, CancellationToken ct = default) =>
        await _dbContext.AnalyticsSessions.AddAsync(session, ct);

    public async Task AddAnalyticsEventsAsync(IEnumerable<AnalyticsEvent> events, CancellationToken ct = default) =>
        await _dbContext.AnalyticsEvents.AddRangeAsync(events, ct);

    public async Task IncrementHourlyMetricAsync(DateTimeOffset hourUtc, string metricKey, long increment, CancellationToken ct = default)
    {
        var hour = new DateTimeOffset(hourUtc.UtcDateTime.Date.AddHours(hourUtc.UtcDateTime.Hour), TimeSpan.Zero);
        var existing = await _dbContext.AnalyticsHourlyMetrics
            .FirstOrDefaultAsync(m => m.HourUtc == hour && m.MetricKey == metricKey, ct);

        if (existing is null)
        {
            await _dbContext.AnalyticsHourlyMetrics.AddAsync(new AnalyticsHourlyMetric
            {
                Id = Guid.NewGuid(),
                HourUtc = hour,
                MetricKey = metricKey,
                Count = increment
            }, ct);
        }
        else
        {
            existing.Count += increment;
        }
    }

    public Task<IReadOnlyList<AnalyticsHourlyMetric>> GetHourlyMetricsSinceAsync(DateTimeOffset since, CancellationToken ct = default) =>
        _dbContext.AnalyticsHourlyMetrics.AsNoTracking()
            .Where(m => m.HourUtc >= since)
            .OrderBy(m => m.HourUtc)
            .ThenBy(m => m.MetricKey)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<AnalyticsHourlyMetric>)t.Result, ct);

    public Task<UiTranslationBundle?> GetUiTranslationBundleAsync(string locale, CancellationToken ct = default) =>
        _dbContext.UiTranslationBundles.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Locale == locale, ct);

    public async Task UpsertUiTranslationBundleAsync(UiTranslationBundle bundle, CancellationToken ct = default)
    {
        var existing = await _dbContext.UiTranslationBundles
            .FirstOrDefaultAsync(b => b.Locale == bundle.Locale && b.Namespace == bundle.Namespace, ct);

        if (existing is null)
        {
            await _dbContext.UiTranslationBundles.AddAsync(bundle, ct);
        }
        else
        {
            existing.PayloadJson = bundle.PayloadJson;
            existing.SourceHash = bundle.SourceHash;
            existing.Status = bundle.Status;
            existing.UpdatedAt = bundle.UpdatedAt;
        }
    }
}

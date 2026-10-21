using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Abstractions.Repositories;

public interface IPlatformRepository
{
    Task<(IReadOnlyList<User> Items, int TotalCount)> ListUsersAsync(int page, int pageSize, CancellationToken ct = default);

    Task<User?> GetUserWithRolesAsync(Guid userId, CancellationToken ct = default);

    Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct = default);

    Task AssignUserRolesAsync(Guid userId, IReadOnlyList<string> roleNames, CancellationToken ct = default);

    Task<IReadOnlyList<Role>> GetAllRolesAsync(CancellationToken ct = default);

    Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default);

    Task UpdateRolePermissionsAsync(Guid roleId, string permissionsJson, CancellationToken ct = default);

    Task<AiUsageLimit?> GetAiUsageForDateAsync(Guid userId, DateTime dateUtc, CancellationToken ct = default);

    Task IncrementAiUsageAsync(Guid userId, DateTime dateUtc, CancellationToken ct = default);

    Task<AnalyticsDevice> GetOrCreateAnalyticsDeviceAsync(string deviceKey, CancellationToken ct = default);

    Task<AnalyticsSession?> GetAnalyticsSessionAsync(Guid sessionId, CancellationToken ct = default);

    Task AddAnalyticsSessionAsync(AnalyticsSession session, CancellationToken ct = default);

    Task AddAnalyticsEventsAsync(IEnumerable<AnalyticsEvent> events, CancellationToken ct = default);

    Task IncrementHourlyMetricAsync(DateTimeOffset hourUtc, string metricKey, long increment, CancellationToken ct = default);

    Task<IReadOnlyList<AnalyticsHourlyMetric>> GetHourlyMetricsSinceAsync(DateTimeOffset since, CancellationToken ct = default);

    Task<UiTranslationBundle?> GetUiTranslationBundleAsync(string locale, CancellationToken ct = default);

    Task UpsertUiTranslationBundleAsync(UiTranslationBundle bundle, CancellationToken ct = default);
}

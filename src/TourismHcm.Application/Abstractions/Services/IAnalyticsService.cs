using TourismHcm.Contracts.Analytics;

namespace TourismHcm.Application.Abstractions.Services;

public interface IAnalyticsService
{
    Task CollectAsync(AnalyticsCollectRequest request, CancellationToken ct = default);

    Task<AnalyticsAdminSummaryDto> GetAdminSummaryAsync(CancellationToken ct = default);

    Task<string> ExportCsvAsync(CancellationToken ct = default);
}

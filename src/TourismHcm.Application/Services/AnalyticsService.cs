using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Analytics;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.Application.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    public static readonly TimeSpan ActiveDeviceWindow = TimeSpan.FromMinutes(3);

    private readonly IPlatformRepository _platformRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AnalyticsService(IPlatformRepository platformRepository, IUnitOfWork unitOfWork)
    {
        _platformRepository = platformRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task CollectAsync(AnalyticsCollectRequest request, CancellationToken ct = default)
    {
        if (!request.Consent)
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                [nameof(request.Consent)] = ["Analytics consent must be granted."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                [nameof(request.DeviceId)] = ["Device id is required."]
            });
        }

        if (request.Events.Count == 0)
        {
            return;
        }

        var deviceHash = ContentHash.Compute(request.DeviceId.Trim());
        var device = await _platformRepository.GetOrCreateAnalyticsDeviceAsync(deviceHash, ct);
        device.ConsentAnalytics = true;
        device.LastSeenAt = DateTimeOffset.UtcNow;

        var session = await _platformRepository.GetAnalyticsSessionAsync(request.SessionId, ct);
        if (session is null)
        {
            session = new AnalyticsSession
            {
                Id = request.SessionId == Guid.Empty ? Guid.NewGuid() : request.SessionId,
                DeviceId = device.Id,
                ConsentAnalytics = true,
                StartedAt = DateTimeOffset.UtcNow
            };
            await _platformRepository.AddAnalyticsSessionAsync(session, ct);
        }

        var now = DateTimeOffset.UtcNow;
        var entities = request.Events.Select(e => new AnalyticsEvent
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            EventName = e.EventName.Trim(),
            PayloadJson = e.PayloadJson,
            ConsentAnalytics = true,
            OccurredAt = e.OccurredAt ?? now
        }).ToList();

        await _platformRepository.AddAnalyticsEventsAsync(entities, ct);

        foreach (var group in entities.GroupBy(e => e.EventName, StringComparer.OrdinalIgnoreCase))
        {
            var hour = new DateTimeOffset(now.UtcDateTime.Date.AddHours(now.UtcDateTime.Hour), TimeSpan.Zero);
            await _platformRepository.IncrementHourlyMetricAsync(hour, group.Key, group.Count(), ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RecordPresenceAsync(string deviceId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                [nameof(deviceId)] = ["Device id is required."]
            });
        }

        var deviceHash = ContentHash.Compute(deviceId.Trim());
        await _platformRepository.TouchAnalyticsDeviceAsync(deviceHash, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<AnalyticsAdminSummaryDto> GetAdminSummaryAsync(CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-48);
        var metrics = await _platformRepository.GetHourlyMetricsSinceAsync(since, ct);
        var activeSince = DateTimeOffset.UtcNow - ActiveDeviceWindow;
        var activeDevices = await _platformRepository.CountAnalyticsDevicesSeenSinceAsync(activeSince, ct);
        return new AnalyticsAdminSummaryDto
        {
            ActiveDevices = activeDevices,
            Hourly = metrics.Select(m => new AnalyticsHourlyMetricDto
            {
                HourUtc = m.HourUtc,
                MetricKey = m.MetricKey,
                Count = m.Count
            }).ToList()
        };
    }

    public async Task<string> ExportCsvAsync(CancellationToken ct = default)
    {
        var summary = await GetAdminSummaryAsync(ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("hour_utc,metric_key,count");
        foreach (var row in summary.Hourly.OrderBy(h => h.HourUtc).ThenBy(h => h.MetricKey))
        {
            sb.Append(row.HourUtc.UtcDateTime.ToString("O")).Append(',')
                .Append(EscapeCsv(row.MetricKey)).Append(',')
                .Append(row.Count).AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}

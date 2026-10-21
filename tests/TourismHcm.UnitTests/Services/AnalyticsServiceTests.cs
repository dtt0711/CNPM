using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Analytics;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class AnalyticsServiceTests
{
    private readonly IPlatformRepository _platform = Substitute.For<IPlatformRepository>();
    private readonly IUnitOfWork _uow = FakeProviders.CreateUnitOfWork();

    private AnalyticsService CreateSut() => new(_platform, _uow);

    [Fact]
    public async Task CollectAsync_WithoutConsent_Throws()
    {
        var sut = CreateSut();
        var act = async () => await sut.CollectAsync(new AnalyticsCollectRequest
        {
            Consent = false,
            DeviceId = "dev-1",
            SessionId = Guid.NewGuid(),
            Events = [new AnalyticsEventInput { EventName = "open" }]
        });

        await act.Should().ThrowAsync<ValidationDomainException>();
    }

    [Fact]
    public async Task CollectAsync_EmptyEvents_DoesNotPersist()
    {
        var sut = CreateSut();
        await sut.CollectAsync(new AnalyticsCollectRequest
        {
            Consent = true,
            DeviceId = "dev-1",
            SessionId = Guid.NewGuid()
        });

        await _platform.DidNotReceive().GetOrCreateAnalyticsDeviceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CollectAsync_PersistsEvents_AndHourlyMetric()
    {
        var device = new AnalyticsDevice { Id = Guid.NewGuid() };
        _platform.GetOrCreateAnalyticsDeviceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(device);
        _platform.GetAnalyticsSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((AnalyticsSession?)null);

        var sut = CreateSut();
        await sut.CollectAsync(new AnalyticsCollectRequest
        {
            Consent = true,
            DeviceId = "phone-a",
            SessionId = Guid.NewGuid(),
            Events = [new AnalyticsEventInput { EventName = "place_open" }]
        });

        await _platform.Received(1).AddAnalyticsEventsAsync(Arg.Any<IEnumerable<AnalyticsEvent>>(), Arg.Any<CancellationToken>());
        await _platform.Received(1).IncrementHourlyMetricAsync(Arg.Any<DateTimeOffset>(), "place_open", 1, Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportCsvAsync_EscapesCommaInKey()
    {
        _platform.GetHourlyMetricsSinceAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([
                new AnalyticsHourlyMetric
                {
                    HourUtc = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero),
                    MetricKey = "place,open",
                    Count = 3
                }
            ]);

        var sut = CreateSut();
        var csv = await sut.ExportCsvAsync();

        csv.Should().Contain("\"place,open\"");
        csv.Should().Contain(",3");
    }
}

using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Options;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Narration;
using TourismHcm.Domain.Enums;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class GeofenceServiceTests
{
    private readonly IPlaceRepository _placeRepository = Substitute.For<IPlaceRepository>();
    private readonly INarrationService _narrationService = Substitute.For<INarrationService>();
    private readonly IDistributedCache _cache;
    private readonly GeofenceOptions _options = new()
    {
        CooldownMinutes = 5,
        DefaultRadiusMeters = 30,
        MaxScanRadiusMeters = 500,
    };

    public GeofenceServiceTests()
    {
        // A real in-memory distributed cache (not a mock) so cooldown TTL/"already set" semantics
        // are exercised faithfully, exactly as they would be in Development/Testing.
        _cache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

        _narrationService
            .GetNarrationAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new NarrationResponse
            {
                PlaceId = call.ArgAt<Guid>(0),
                Name = "Narrated Place",
                RequestedLang = call.ArgAt<string>(1),
                ActualLang = call.ArgAt<string>(1),
                IsFallback = false,
                Text = "Some narration text.",
            }));
    }

    private GeofenceService CreateSut() => new(
        _placeRepository, _narrationService, _cache, OptionsFactory.Create(_options));

    [Fact]
    public async Task TriggerAsync_WhenClientIsInsideRadius_ReturnsTriggeredResponse()
    {
        var place = EntityFactory.CreatePlace(latitude: 10.0, longitude: 106.0, triggerRadiusMeters: 100);
        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<PlaceStatus?>(), Arg.Any<CancellationToken>())
            .Returns([place]);

        var sut = CreateSut();

        // Same coordinates as the place => distance 0m, well inside the 100m radius.
        var result = await sut.TriggerAsync(new TriggerRequest { Latitude = 10.0, Longitude = 106.0, Lang = "en" });

        result.Should().NotBeNull();
        result!.Triggered.Should().BeTrue();
        result.PlaceId.Should().Be(place.Id);
        result.DistanceMeters.Should().Be(0);
        result.Narration.Should().NotBeNull();
    }

    [Fact]
    public async Task TriggerAsync_WhenClientIsOutsideRadius_ReturnsNull()
    {
        var place = EntityFactory.CreatePlace(latitude: 10.0, longitude: 106.0, triggerRadiusMeters: 30);
        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<PlaceStatus?>(), Arg.Any<CancellationToken>())
            .Returns([place]);

        var sut = CreateSut();

        // ~1.1km away (0.01 degrees latitude), far outside the 30m trigger radius.
        var result = await sut.TriggerAsync(new TriggerRequest { Latitude = 10.01, Longitude = 106.0, Lang = "en" });

        result.Should().BeNull();
    }

    [Fact]
    public async Task TriggerAsync_WhenMultiplePlacesOverlap_PicksNearestPlace()
    {
        var farPlace = EntityFactory.CreatePlace(name: "Far", latitude: 10.002, longitude: 106.0, triggerRadiusMeters: 500);
        var nearPlace = EntityFactory.CreatePlace(name: "Near", latitude: 10.0005, longitude: 106.0, triggerRadiusMeters: 500);

        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<PlaceStatus?>(), Arg.Any<CancellationToken>())
            .Returns([farPlace, nearPlace]);

        var sut = CreateSut();

        var result = await sut.TriggerAsync(new TriggerRequest { Latitude = 10.0, Longitude = 106.0, Lang = "en" });

        result.Should().NotBeNull();
        result!.PlaceId.Should().Be(nearPlace.Id);
        result.Name.Should().Be("Near");
    }

    [Fact]
    public async Task TriggerAsync_SecondCallWithinCooldownWindow_ReturnsTriggeredFalse()
    {
        var place = EntityFactory.CreatePlace(latitude: 10.0, longitude: 106.0, triggerRadiusMeters: 100);
        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<PlaceStatus?>(), Arg.Any<CancellationToken>())
            .Returns([place]);

        var sut = CreateSut();
        var request = new TriggerRequest { Latitude = 10.0, Longitude = 106.0, Lang = "en", ClientId = "device-1" };

        var first = await sut.TriggerAsync(request);
        first.Should().NotBeNull();
        first!.Triggered.Should().BeTrue("the very first trigger within range should fire");

        // Second call, same client + place, still well within the 5-minute cooldown window.
        var second = await sut.TriggerAsync(request);

        second.Should().NotBeNull();
        second!.Triggered.Should().BeFalse("a repeat trigger within the cooldown window must be suppressed");
        second.CooldownActive.Should().BeTrue();
        second.PlaceId.Should().Be(place.Id);
    }

    [Fact]
    public async Task TriggerAsync_DifferentClientIds_EachGetTheirOwnCooldown()
    {
        var place = EntityFactory.CreatePlace(latitude: 10.0, longitude: 106.0, triggerRadiusMeters: 100);
        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<PlaceStatus?>(), Arg.Any<CancellationToken>())
            .Returns([place]);

        var sut = CreateSut();

        var first = await sut.TriggerAsync(new TriggerRequest { Latitude = 10.0, Longitude = 106.0, ClientId = "device-A" });
        var second = await sut.TriggerAsync(new TriggerRequest { Latitude = 10.0, Longitude = 106.0, ClientId = "device-B" });

        first!.Triggered.Should().BeTrue();
        second!.Triggered.Should().BeTrue("a different client has an independent cooldown bucket");
    }

    [Fact]
    public async Task TriggerAsync_WhenPlaceRadiusIsZero_UsesConfiguredDefaultRadius()
    {
        // Radius 0 is not a real fence; the service substitutes GeofenceOptions.DefaultRadiusMeters (30).
        var place = EntityFactory.CreatePlace(latitude: 10.0, longitude: 106.0, triggerRadiusMeters: 0);
        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<PlaceStatus?>(), Arg.Any<CancellationToken>())
            .Returns([place]);

        var sut = CreateSut();

        var inside = await sut.TriggerAsync(new TriggerRequest { Latitude = 10.0, Longitude = 106.0, ClientId = "radius-default" });
        inside.Should().NotBeNull();
        inside!.Triggered.Should().BeTrue();

        // ~111m north, outside the 30m default radius.
        var outside = await sut.TriggerAsync(new TriggerRequest
        {
            Latitude = 10.001,
            Longitude = 106.0,
            ClientId = "radius-default-far",
        });
        outside.Should().BeNull();
    }

    [Fact]
    public async Task TriggerAsync_BlankClientId_SharesTheAnonymousCooldownBucket()
    {
        var place = EntityFactory.CreatePlace(latitude: 10.0, longitude: 106.0, triggerRadiusMeters: 100);
        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<PlaceStatus?>(), Arg.Any<CancellationToken>())
            .Returns([place]);

        var sut = CreateSut();

        var first = await sut.TriggerAsync(new TriggerRequest { Latitude = 10.0, Longitude = 106.0, ClientId = "   " });
        var second = await sut.TriggerAsync(new TriggerRequest { Latitude = 10.0, Longitude = 106.0, ClientId = null });

        first!.Triggered.Should().BeTrue();
        second!.Triggered.Should().BeFalse("a missing client id and a blank one both map to the anonymous bucket");
    }

    [Fact]
    public async Task TriggerAsync_WhenNoCandidatePlaces_ReturnsNull()
    {
        _placeRepository
            .GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<PlaceStatus?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var sut = CreateSut();

        var result = await sut.TriggerAsync(new TriggerRequest { Latitude = 10.0, Longitude = 106.0 });

        result.Should().BeNull();
    }
}

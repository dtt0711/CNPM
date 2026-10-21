using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Audio;
using TourismHcm.Contracts.Narration;
using TourismHcm.Domain.Entities;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class LocalizationServiceTests
{
    private readonly IAudioService _audio = Substitute.For<IAudioService>();
    private readonly INarrationService _narration = Substitute.For<INarrationService>();
    private readonly IPlaceRepository _places = Substitute.For<IPlaceRepository>();
    private readonly IDistributedCache _cache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private LocalizationService CreateSut() => new(_audio, _narration, _places, _cache);

    [Fact]
    public async Task OnDemandAsync_WhenAudioReady_ReturnsReady()
    {
        var placeId = Guid.NewGuid();
        _narration.GetNarrationAsync(placeId, "en", Arg.Any<CancellationToken>())
            .Returns(new NarrationResponse
            {
                PlaceId = placeId,
                ActualLang = "en",
                Audio = new AudioInfoDto { Available = true, Url = "/audio/en.mp3" }
            });

        var result = await CreateSut().OnDemandAsync(placeId, "en");

        result.Status.Should().Be("ready");
        result.AudioUrl.Should().Be("/audio/en.mp3");
        await _audio.DidNotReceive().RequestAudioAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnDemandAsync_WhenMissing_QueuesTask()
    {
        var placeId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _narration.GetNarrationAsync(placeId, "ja", Arg.Any<CancellationToken>())
            .Returns(new NarrationResponse
            {
                PlaceId = placeId,
                ActualLang = "ja",
                Audio = new AudioInfoDto { Available = false }
            });
        _audio.RequestAudioAsync(placeId, "ja", Arg.Any<CancellationToken>())
            .Returns(new AudioTaskDto { Id = taskId, Status = "Pending" });

        var result = await CreateSut().OnDemandAsync(placeId, "ja");

        result.Status.Should().Be("Pending");
        result.TaskId.Should().Be(taskId);
    }

    [Fact]
    public async Task WarmupAsync_CountsReadyAndQueued()
    {
        var readyId = Guid.NewGuid();
        var queuedId = Guid.NewGuid();
        _narration.GetNarrationAsync(readyId, "en", Arg.Any<CancellationToken>())
            .Returns(new NarrationResponse { Audio = new AudioInfoDto { Available = true, Url = "/a.mp3" } });
        _narration.GetNarrationAsync(queuedId, "en", Arg.Any<CancellationToken>())
            .Returns(new NarrationResponse { Audio = new AudioInfoDto { Available = false } });
        _audio.RequestAudioAsync(queuedId, "en", Arg.Any<CancellationToken>())
            .Returns(new AudioTaskDto { Id = Guid.NewGuid(), Status = "Pending" });

        var result = await CreateSut().WarmupAsync("en", [readyId, queuedId]);

        result.AlreadyReady.Should().Be(1);
        result.Queued.Should().Be(1);
    }

    [Fact]
    public async Task PrepareHotsetAsync_RanksNearbyPlaces()
    {
        var near = EntityFactory.CreatePlace(latitude: 10.78, longitude: 106.70);
        var far = EntityFactory.CreatePlace(latitude: 11.0, longitude: 107.0);
        _places.GetInBoundingBoxAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<TourismHcm.Domain.Enums.PlaceStatus?>(), Arg.Any<CancellationToken>())
            .Returns(new List<Place> { near, far });
        _narration.GetNarrationAsync(near.Id, "vi", Arg.Any<CancellationToken>())
            .Returns(new NarrationResponse
            {
                Text = "ok",
                Audio = new AudioInfoDto { Available = true, Url = "/a.mp3" }
            });

        var result = await CreateSut().PrepareHotsetAsync(10.78, 106.70, "vi");

        result.PlaceIds.Should().Contain(near.Id);
        result.PlaceIds.Should().NotContain(far.Id);
        result.ReadyCount.Should().Be(1);
    }
}

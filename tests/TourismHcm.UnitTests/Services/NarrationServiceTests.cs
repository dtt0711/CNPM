using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Audio;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class NarrationServiceTests
{
    private readonly IPlaceRepository _placeRepository = Substitute.For<IPlaceRepository>();
    private readonly ITranslationRepository _translationRepository = Substitute.For<ITranslationRepository>();
    private readonly IAudioAssetRepository _audioAssetRepository = Substitute.For<IAudioAssetRepository>();
    private readonly IAudioStorage _audioStorage = Substitute.For<IAudioStorage>();
    private readonly IAudioService _audioService = Substitute.For<IAudioService>();
    private readonly ILogger<NarrationService> _logger = Substitute.For<ILogger<NarrationService>>();

    private NarrationService CreateSut()
    {
        _audioService.EnsureTranslationAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _audioService.RequestAudioAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AudioTaskDto
            {
                Id = Guid.NewGuid(),
                PlaceId = Guid.Empty,
                Lang = "en",
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        _audioService.GetTaskStatusAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AudioTaskDto?)null);

        return new(
            _placeRepository,
            _translationRepository,
            _audioAssetRepository,
            _audioStorage,
            _audioService,
            _logger);
    }

    [Fact]
    public async Task GetNarrationAsync_WhenPlaceDoesNotExist_ThrowsNotFound()
    {
        _placeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TourismHcm.Domain.Entities.Place?)null);

        var sut = CreateSut();

        var act = async () => await sut.GetNarrationAsync(Guid.NewGuid(), "en");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetNarrationAsync_WhenRequestedLanguageTranslationExists_ReturnsItWithoutFallback()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var translation = EntityFactory.CreateTranslation(place.Id, "en", text: "English description.");
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>()).Returns(translation);

        _audioAssetRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>())
            .Returns((TourismHcm.Domain.Entities.AudioAsset?)null);

        var sut = CreateSut();

        var result = await sut.GetNarrationAsync(place.Id, "en");

        result.ActualLang.Should().Be("en");
        result.RequestedLang.Should().Be("en");
        result.IsFallback.Should().BeFalse();
        result.Text.Should().Be("English description.");
    }

    [Fact]
    public async Task GetNarrationAsync_WhenRequestedLanguageMissing_FallsBackToEnglish()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        // No translation for the requested "ja", but English is available.
        _translationRepository.GetAsync(place.Id, "ja", Arg.Any<CancellationToken>())
            .Returns((TourismHcm.Domain.Entities.PlaceTranslation?)null);

        var englishTranslation = EntityFactory.CreateTranslation(place.Id, "en", text: "English fallback text.");
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>()).Returns(englishTranslation);

        _audioAssetRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>())
            .Returns((TourismHcm.Domain.Entities.AudioAsset?)null);

        var sut = CreateSut();

        var result = await sut.GetNarrationAsync(place.Id, "ja");

        result.RequestedLang.Should().Be("ja");
        result.ActualLang.Should().Be("en");
        result.IsFallback.Should().BeTrue();
        result.Text.Should().Be("English fallback text.");
    }

    [Fact]
    public async Task GetNarrationAsync_WhenRequestedTranslationIsWhitespace_FallsBackToEnglish()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        _translationRepository.GetAsync(place.Id, "ja", Arg.Any<CancellationToken>())
            .Returns(EntityFactory.CreateTranslation(place.Id, "ja", text: "   "));
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>())
            .Returns(EntityFactory.CreateTranslation(place.Id, "en", text: "English after blank Japanese."));
        _audioAssetRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>())
            .Returns((TourismHcm.Domain.Entities.AudioAsset?)null);

        var sut = CreateSut();
        var result = await sut.GetNarrationAsync(place.Id, "JA");

        result.RequestedLang.Should().Be("ja");
        result.ActualLang.Should().Be("en");
        result.IsFallback.Should().BeTrue();
        result.Text.Should().Be("English after blank Japanese.");
    }

    [Fact]
    public async Task GetNarrationAsync_WhenRequestedAndEnglishMissing_FallsBackToVietnamese()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung tiếng Việt gốc.");
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        // Neither the requested language ("ja") nor English have a translation row.
        _translationRepository.GetAsync(place.Id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TourismHcm.Domain.Entities.PlaceTranslation?)null);

        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>())
            .Returns((TourismHcm.Domain.Entities.AudioAsset?)null);

        var sut = CreateSut();

        var result = await sut.GetNarrationAsync(place.Id, "ja");

        result.ActualLang.Should().Be("vi");
        result.IsFallback.Should().BeTrue();
        result.Text.Should().Be(place.DescriptionVi);
    }

    [Fact]
    public async Task GetNarrationAsync_WhenRequestingVietnamese_UsesPlaceDescriptionDirectly_NoFallback()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung gốc.");
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>())
            .Returns((TourismHcm.Domain.Entities.AudioAsset?)null);

        var sut = CreateSut();

        var result = await sut.GetNarrationAsync(place.Id, "vi");

        result.ActualLang.Should().Be("vi");
        result.IsFallback.Should().BeFalse();
        result.Text.Should().Be("Nội dung gốc.");

        // Vietnamese never needs a translation lookup, it lives on the Place entity directly.
        await _translationRepository.DidNotReceive().GetAsync(Arg.Any<Guid>(), "vi", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNarrationAsync_WhenNoAudioAssetExists_ReturnsNotRequestedAudioInfo()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>())
            .Returns((TourismHcm.Domain.Entities.AudioAsset?)null);

        var sut = CreateSut();

        var result = await sut.GetNarrationAsync(place.Id, "vi");

        result.Audio.Available.Should().BeFalse();
        result.Audio.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task GetNarrationAsync_WhenAudioAssetPending_ReturnsUnavailableWithStatus()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var asset = EntityFactory.CreateAudioAsset(place.Id, "vi", status: AudioAssetStatus.Pending);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns(asset);

        var sut = CreateSut();

        var result = await sut.GetNarrationAsync(place.Id, "vi");

        result.Audio.Available.Should().BeFalse();
        result.Audio.Status.Should().Be("Pending");
        result.Audio.DurationSec.Should().Be(asset.DurationSec);
    }

    [Fact]
    public async Task GetNarrationAsync_WhenAudioAssetReady_ReturnsAvailableWithPublicUrl()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var asset = EntityFactory.CreateAudioAsset(place.Id, "vi", status: AudioAssetStatus.Ready);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns(asset);
        _audioStorage.GetPublicUrl(asset.Path).Returns($"/audio/{asset.Path}");

        var sut = CreateSut();

        var result = await sut.GetNarrationAsync(place.Id, "vi");

        result.Audio.Available.Should().BeTrue();
        result.Audio.Url.Should().Be($"/audio/{asset.Path}");
        result.Audio.DurationSec.Should().Be(asset.DurationSec);
    }
}

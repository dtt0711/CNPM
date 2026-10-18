using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Services;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class OfflinePackServiceTests
{
    private readonly IPlaceRepository _placeRepository = Substitute.For<IPlaceRepository>();
    private readonly ITranslationRepository _translationRepository = Substitute.For<ITranslationRepository>();
    private readonly IAudioAssetRepository _audioAssetRepository = Substitute.For<IAudioAssetRepository>();
    private readonly TourismHcm.Application.Abstractions.Providers.IAudioStorage _audioStorage = FakeProviders.CreateAudioStorage();

    private OfflinePackService CreateSut() =>
        new(_placeRepository, _translationRepository, _audioAssetRepository, _audioStorage);

    [Fact]
    public async Task BuildManifestAsync_WithNoPlaces_ReturnsEmptyManifest()
    {
        _placeRepository.GetAllPublishedAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Place>)[]);

        var sut = CreateSut();
        var manifest = await sut.BuildManifestAsync();

        manifest.Places.Should().BeEmpty();
        manifest.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildManifestAsync_IncludesVietnameseTextDirectlyFromPlace()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Mô tả gốc.");
        _placeRepository.GetAllPublishedAsync(Arg.Any<CancellationToken>()).Returns([place]);
        _translationRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PlaceTranslation?)null);
        _audioAssetRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AudioAsset?)null);

        var sut = CreateSut();
        var manifest = await sut.BuildManifestAsync(["vi"]);

        manifest.Places.Should().ContainSingle();
        manifest.Places[0].Texts.Should().ContainKey("vi").WhoseValue.Should().Be("Mô tả gốc.");
    }

    [Fact]
    public async Task BuildManifestAsync_IncludesOnlyReadyAudioAssetUrls()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetAllPublishedAsync(Arg.Any<CancellationToken>()).Returns([place]);
        _translationRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PlaceTranslation?)null);

        var readyAsset = EntityFactory.CreateAudioAsset(place.Id, "en", status: AudioAssetStatus.Ready);
        _audioAssetRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>()).Returns(readyAsset);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((AudioAsset?)null);
        _audioStorage.GetPublicUrl(readyAsset.Path).Returns($"/audio/{readyAsset.Path}");

        var sut = CreateSut();
        var manifest = await sut.BuildManifestAsync(["vi", "en"]);

        manifest.Places[0].AudioUrls.Should().ContainKey("en");
        manifest.Places[0].AudioUrls.Should().NotContainKey("vi");
    }

    [Fact]
    public async Task BuildManifestAsync_WithNoLangsRequested_DefaultsToPriorityLanguages()
    {
        _placeRepository.GetAllPublishedAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Place>)[]);

        var sut = CreateSut();
        var manifest = await sut.BuildManifestAsync(null);

        manifest.Should().NotBeNull();
        await _placeRepository.Received(1).GetAllPublishedAsync(Arg.Any<CancellationToken>());
    }
}

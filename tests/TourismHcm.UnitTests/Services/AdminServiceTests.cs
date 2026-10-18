using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Admin;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class AdminServiceTests
{
    private readonly IPlaceRepository _placeRepository = Substitute.For<IPlaceRepository>();
    private readonly ITranslationRepository _translationRepository = Substitute.For<ITranslationRepository>();
    private readonly IAuditLogRepository _auditLogRepository = Substitute.For<IAuditLogRepository>();
    private readonly IAudioService _audioService = Substitute.For<IAudioService>();
    private readonly IPlaceService _placeService = Substitute.For<IPlaceService>();
    private readonly IUnitOfWork _unitOfWork = FakeProviders.CreateUnitOfWork();

    private AdminService CreateSut() => new(
        _placeRepository, _translationRepository, _auditLogRepository, _audioService, _placeService, _unitOfWork);

    [Fact]
    public async Task ApprovePlaceAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Place?)null);

        var sut = CreateSut();
        var act = async () => await sut.ApprovePlaceAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ApprovePlaceAsync_SetsStatusPublished_AndWritesAuditLog()
    {
        var place = EntityFactory.CreatePlace(status: PlaceStatus.Draft);
        var adminId = Guid.NewGuid();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _placeService.GetByIdAsync(place.Id, Arg.Any<CancellationToken>())
            .Returns(new PlaceDetailDto { Id = place.Id, Status = PlaceStatus.Published.ToString() });

        var sut = CreateSut();
        var result = await sut.ApprovePlaceAsync(place.Id, adminId);

        place.Status.Should().Be(PlaceStatus.Published);
        result.Status.Should().Be(PlaceStatus.Published.ToString());

        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<AuditLog>(a => a.Action == "ApprovePlace" && a.UserId == adminId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HidePlaceAsync_SetsStatusHidden_AndWritesAuditLog()
    {
        var place = EntityFactory.CreatePlace(status: PlaceStatus.Published);
        var adminId = Guid.NewGuid();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _placeService.GetByIdAsync(place.Id, Arg.Any<CancellationToken>())
            .Returns(new PlaceDetailDto { Id = place.Id, Status = PlaceStatus.Hidden.ToString() });

        var sut = CreateSut();
        await sut.HidePlaceAsync(place.Id, adminId);

        place.Status.Should().Be(PlaceStatus.Hidden);
        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<AuditLog>(a => a.Action == "HidePlace"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTranslationAsync_WhenLanguageUnsupported_ThrowsValidationDomainException()
    {
        var sut = CreateSut();
        var request = new UpdateTranslationRequest { PlaceId = Guid.NewGuid(), Lang = "xx", Text = "..." };

        var act = async () => await sut.UpdateTranslationAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationDomainException>();
    }

    [Fact]
    public async Task UpdateTranslationAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Place?)null);
        var sut = CreateSut();
        var request = new UpdateTranslationRequest { PlaceId = Guid.NewGuid(), Lang = "en", Text = "..." };

        var act = async () => await sut.UpdateTranslationAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateTranslationAsync_WhenLangIsVietnamese_UpdatesPlaceDescriptionDirectly()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Old text.");
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var sut = CreateSut();
        await sut.UpdateTranslationAsync(Guid.NewGuid(), new UpdateTranslationRequest
        {
            PlaceId = place.Id,
            Lang = "vi",
            Text = "  Manually corrected text.  ",
        });

        place.DescriptionVi.Should().Be("Manually corrected text.");
        place.ContentHash.Should().Be(TourismHcm.Domain.Helpers.ContentHash.Compute(place.DescriptionVi));
    }

    [Fact]
    public async Task UpdateTranslationAsync_WhenNoExistingTranslation_CreatesManualTranslation()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>())
            .Returns((PlaceTranslation?)null);

        var sut = CreateSut();
        await sut.UpdateTranslationAsync(Guid.NewGuid(), new UpdateTranslationRequest
        {
            PlaceId = place.Id,
            Lang = "en",
            Text = "Manual English text.",
        });

        await _translationRepository.Received(1).AddAsync(
            Arg.Is<PlaceTranslation>(t => t.Text == "Manual English text." && t.Source == TranslationSource.Manual),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTranslationAsync_WhenExistingTranslation_OverwritesAsManual()
    {
        var place = EntityFactory.CreatePlace();
        var existing = EntityFactory.CreateTranslation(place.Id, "en", source: TranslationSource.Machine);
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>()).Returns(existing);

        var sut = CreateSut();
        await sut.UpdateTranslationAsync(Guid.NewGuid(), new UpdateTranslationRequest
        {
            PlaceId = place.Id,
            Lang = "en",
            Text = "Overridden by admin.",
        });

        existing.Text.Should().Be("Overridden by admin.");
        existing.Source.Should().Be(TranslationSource.Manual);
    }

    [Fact]
    public async Task RegenerateAudioAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Place?)null);
        var sut = CreateSut();

        var act = async () => await sut.RegenerateAudioAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RegenerateAudioAsync_WhenPlaceExists_DelegatesToAudioService_AndLogsAudit()
    {
        var place = EntityFactory.CreatePlace();
        var adminId = Guid.NewGuid();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var sut = CreateSut();
        await sut.RegenerateAudioAsync(place.Id, adminId);

        await _audioService.Received(1).RegenerateAllLanguagesAsync(place.Id, Arg.Any<CancellationToken>());
        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<AuditLog>(a => a.Action == "RegenerateAudio" && a.UserId == adminId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuditLogsAsync_UsesDefaultsForInvalidPaging()
    {
        _auditLogRepository
            .SearchAsync(null, null, 1, 50, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<AuditLog>)[], 0));

        var sut = CreateSut();
        var result = await sut.GetAuditLogsAsync(null, null, page: -5, pageSize: 1000);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);
    }
}

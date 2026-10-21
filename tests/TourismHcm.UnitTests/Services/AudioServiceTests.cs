using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Options;
using TourismHcm.Application.Services;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class AudioServiceTests
{
    private readonly IPlaceRepository _placeRepository = Substitute.For<IPlaceRepository>();
    private readonly ITranslationRepository _translationRepository = Substitute.For<ITranslationRepository>();
    private readonly IAudioAssetRepository _audioAssetRepository = Substitute.For<IAudioAssetRepository>();
    private readonly IAudioTaskRepository _audioTaskRepository = Substitute.For<IAudioTaskRepository>();
    private ITranslationProvider _translationProvider = FakeProviders.CreateTranslationProvider(isConfigured: true);
    private ITextToSpeechProvider _ttsProvider = FakeProviders.CreateTextToSpeechProvider(isConfigured: true);
    private readonly IAudioStorage _audioStorage = FakeProviders.CreateAudioStorage();
    private readonly IAudioTaskQueue _audioTaskQueue = FakeProviders.CreateAudioTaskQueue();
    private readonly IAudioTaskRuntime _audioTaskRuntime = Substitute.For<IAudioTaskRuntime>();
    private readonly IUnitOfWork _unitOfWork = FakeProviders.CreateUnitOfWork();
    private readonly AudioOptions _options = new();

    private AudioService CreateSut()
    {
        _audioTaskRuntime.Register(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1));

        return new(
            _placeRepository,
            _translationRepository,
            _audioAssetRepository,
            _audioTaskRepository,
            _translationProvider,
            _ttsProvider,
            _audioStorage,
            _audioTaskQueue,
            _audioTaskRuntime,
            _unitOfWork,
            OptionsFactory.Create(_options),
            NullLogger<AudioService>.Instance);
    }

    // ---------- EnsureTranslationAsync ----------

    [Fact]
    public async Task EnsureTranslationAsync_WhenLangIsVietnamese_ReturnsImmediately_NoLookups()
    {
        var sut = CreateSut();

        await sut.EnsureTranslationAsync(Guid.NewGuid(), "vi");

        await _placeRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureTranslationAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Place?)null);
        var sut = CreateSut();

        var act = async () => await sut.EnsureTranslationAsync(Guid.NewGuid(), "en");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task EnsureTranslationAsync_WhenExistingTranslationIsManual_NeverOverwritten()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var manual = EntityFactory.CreateTranslation(place.Id, "en", source: TranslationSource.Manual, contentHash: "stale-hash");
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>()).Returns(manual);

        var sut = CreateSut();
        await sut.EnsureTranslationAsync(place.Id, "en");

        await _translationProvider.DidNotReceive().TranslateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureTranslationAsync_WhenExistingTranslationHashMatches_SkipsTranslation()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var upToDate = EntityFactory.CreateTranslation(place.Id, "en", source: TranslationSource.Machine, contentHash: place.ContentHash);
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>()).Returns(upToDate);

        var sut = CreateSut();
        await sut.EnsureTranslationAsync(place.Id, "en");

        await _translationProvider.DidNotReceive().TranslateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureTranslationAsync_WhenProviderNotConfigured_ThrowsProviderUnavailable()
    {
        _translationProvider = FakeProviders.CreateTranslationProvider(isConfigured: false);
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>())
            .Returns((PlaceTranslation?)null);

        var sut = CreateSut();
        var act = async () => await sut.EnsureTranslationAsync(place.Id, "en");

        await act.Should().ThrowAsync<ProviderUnavailableException>();
    }

    [Fact]
    public async Task EnsureTranslationAsync_WhenNoExistingTranslation_CreatesNewMachineTranslation()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>())
            .Returns((PlaceTranslation?)null);

        var sut = CreateSut();
        await sut.EnsureTranslationAsync(place.Id, "en");

        await _translationRepository.Received(1).AddAsync(
            Arg.Is<PlaceTranslation>(t => t.PlaceId == place.Id && t.Lang == "en" && t.Source == TranslationSource.Machine),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureTranslationAsync_WhenExistingMachineTranslationIsStale_UpdatesInPlace()
    {
        var place = EntityFactory.CreatePlace();
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var stale = EntityFactory.CreateTranslation(place.Id, "en", source: TranslationSource.Machine, contentHash: "old-hash");
        _translationRepository.GetAsync(place.Id, "en", Arg.Any<CancellationToken>()).Returns(stale);

        var sut = CreateSut();
        await sut.EnsureTranslationAsync(place.Id, "en");

        stale.ContentHash.Should().Be(place.ContentHash);
        await _translationRepository.DidNotReceive().AddAsync(Arg.Any<PlaceTranslation>(), Arg.Any<CancellationToken>());
    }

    // ---------- RequestAudioAsync ----------

    [Fact]
    public async Task RequestAudioAsync_WhenPlaceMissing_ThrowsNotFound()
    {
        _placeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Place?)null);

        var sut = CreateSut();
        var act = async () => await sut.RequestAudioAsync(Guid.NewGuid(), "vi");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RequestAudioAsync_WhenTextExceedsMaxLength_HashesTheTruncatedText()
    {
        _options.MaxTextLength = 12;
        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung rất dài cần được cắt bớt trước khi băm.");
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((AudioAsset?)null);
        _audioTaskRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((AudioGenerationTask?)null);

        var sut = CreateSut();
        await sut.RequestAudioAsync(place.Id, "vi");

        var expectedHash = TourismHcm.Domain.Helpers.ContentHash.Compute(place.DescriptionVi[.._options.MaxTextLength]);
        await _audioTaskRepository.Received(1).AddAsync(
            Arg.Is<AudioGenerationTask>(t => t.ContentHash == expectedHash),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAudioAsync_WhenReadyAssetMatchesCurrentContent_ReturnsDoneWithoutNewTask()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung ổn định.");
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _translationRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((PlaceTranslation?)null);

        var contentHash = TourismHcm.Domain.Helpers.ContentHash.Compute(place.DescriptionVi);
        var readyAsset = EntityFactory.CreateAudioAsset(place.Id, "vi", status: AudioAssetStatus.Ready, contentHash: contentHash);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns(readyAsset);

        var sut = CreateSut();
        var result = await sut.RequestAudioAsync(place.Id, "vi");

        result.Status.Should().Be(AudioTaskStatus.Done.ToString());
        await _audioTaskRepository.DidNotReceive().AddAsync(Arg.Any<AudioGenerationTask>(), Arg.Any<CancellationToken>());
        await _audioTaskQueue.DidNotReceive().EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAudioAsync_WhenMatchingPendingTaskExists_ReturnsExistingTaskWithoutDuplicating()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung ổn định.");
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _translationRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((PlaceTranslation?)null);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((AudioAsset?)null);

        var contentHash = TourismHcm.Domain.Helpers.ContentHash.Compute(place.DescriptionVi);
        var existingTask = EntityFactory.CreateAudioTask(place.Id, "vi", status: AudioTaskStatus.Pending, contentHash: contentHash);
        _audioTaskRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns(existingTask);

        var sut = CreateSut();
        var result = await sut.RequestAudioAsync(place.Id, "vi");

        result.Id.Should().Be(existingTask.Id);
        await _audioTaskRepository.DidNotReceive().AddAsync(Arg.Any<AudioGenerationTask>(), Arg.Any<CancellationToken>());
        await _audioTaskQueue.DidNotReceive().EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAudioAsync_WhenNoExistingWork_CreatesNewTaskAndEnqueuesIt()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung mới.");
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _translationRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((PlaceTranslation?)null);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((AudioAsset?)null);
        _audioTaskRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((AudioGenerationTask?)null);

        var sut = CreateSut();
        var result = await sut.RequestAudioAsync(place.Id, "vi");

        result.Status.Should().Be(AudioTaskStatus.Pending.ToString());
        await _audioTaskRepository.Received(1).AddAsync(
            Arg.Is<AudioGenerationTask>(t => t.PlaceId == place.Id && t.Lang == "vi"), Arg.Any<CancellationToken>());
        await _audioTaskQueue.Received(1).EnqueueAsync(result.Id, Arg.Any<CancellationToken>());
    }

    // ---------- StreamAsync ----------

    [Fact]
    public async Task StreamAsync_WhenAssetReady_ReturnsReadableStream()
    {
        var placeId = Guid.NewGuid();
        var asset = EntityFactory.CreateAudioAsset(placeId, "en", status: AudioAssetStatus.Ready);
        _audioAssetRepository.GetAsync(placeId, "en", Arg.Any<CancellationToken>()).Returns(asset);

        using var contentStream = new MemoryStream([1, 2, 3]);
        _audioStorage.OpenReadAsync(asset.Path, Arg.Any<CancellationToken>()).Returns((Stream?)contentStream);

        var sut = CreateSut();
        var result = await sut.StreamAsync(placeId, "en");

        result.Content.Should().BeSameAs(contentStream);
        result.DurationSec.Should().Be(asset.DurationSec);
        result.FromCache.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_WhenStorageReturnsNull_ThrowsNotFound()
    {
        var placeId = Guid.NewGuid();
        var asset = EntityFactory.CreateAudioAsset(placeId, "en", status: AudioAssetStatus.Ready);
        _audioAssetRepository.GetAsync(placeId, "en", Arg.Any<CancellationToken>()).Returns(asset);
        _audioStorage.OpenReadAsync(asset.Path, Arg.Any<CancellationToken>()).Returns((Stream?)null);

        var sut = CreateSut();
        var act = async () => await sut.StreamAsync(placeId, "en");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task StreamAsync_WhenAssetDoesNotExist_ThrowsNotFound()
    {
        _audioAssetRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AudioAsset?)null);

        var sut = CreateSut();
        var act = async () => await sut.StreamAsync(Guid.NewGuid(), "en");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task StreamAsync_WhenAssetNotReady_ThrowsNotFound()
    {
        var placeId = Guid.NewGuid();
        var asset = EntityFactory.CreateAudioAsset(placeId, "en", status: AudioAssetStatus.Pending);
        _audioAssetRepository.GetAsync(placeId, "en", Arg.Any<CancellationToken>()).Returns(asset);

        var sut = CreateSut();
        var act = async () => await sut.StreamAsync(placeId, "en");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---------- RegenerateAllLanguagesAsync ----------

    [Fact]
    public async Task RegenerateAllLanguagesAsync_CreatesATaskForEveryPriorityLanguage()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung đa ngôn ngữ.");
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _translationRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PlaceTranslation?)null);
        _audioAssetRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AudioAsset?)null);
        _audioTaskRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AudioGenerationTask?)null);

        var sut = CreateSut();
        await sut.RegenerateAllLanguagesAsync(place.Id);

        await _audioTaskRepository.Received(_options.PriorityLanguages.Length)
            .AddAsync(Arg.Any<AudioGenerationTask>(), Arg.Any<CancellationToken>());
        await _audioTaskQueue.Received(_options.PriorityLanguages.Length)
            .EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegenerateAllLanguagesAsync_WhenTranslationUnavailable_SwallowsDomainExceptionsAndContinues()
    {
        // Vietnamese needs no translation and succeeds; every other priority language requires a
        // translation, and the provider is unconfigured, so each of those requests should throw
        // internally (and be swallowed) without aborting the whole regeneration loop.
        _translationProvider = FakeProviders.CreateTranslationProvider(isConfigured: false);

        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung đa ngôn ngữ.");
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _translationRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PlaceTranslation?)null);
        _audioAssetRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AudioAsset?)null);
        _audioTaskRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AudioGenerationTask?)null);

        var sut = CreateSut();

        // Must not throw: ProviderUnavailableException (a DomainException) is caught per-language.
        await sut.RegenerateAllLanguagesAsync(place.Id);

        await _audioTaskRepository.Received(1).AddAsync(
            Arg.Is<AudioGenerationTask>(t => t.Lang == "vi"), Arg.Any<CancellationToken>());
        await _audioTaskQueue.Received(1).EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ---------- GetTaskStatusAsync ----------

    [Fact]
    public async Task GetTaskStatusAsync_WhenTaskMissing_ReturnsNull()
    {
        _audioTaskRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AudioGenerationTask?)null);

        var sut = CreateSut();
        var result = await sut.GetTaskStatusAsync(Guid.NewGuid(), "en");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTaskStatusAsync_WhenTaskExists_ReturnsMappedDto()
    {
        var placeId = Guid.NewGuid();
        var task = EntityFactory.CreateAudioTask(placeId, "en", status: AudioTaskStatus.Running);
        _audioTaskRepository.GetAsync(placeId, "en", Arg.Any<CancellationToken>()).Returns(task);

        var sut = CreateSut();
        var result = await sut.GetTaskStatusAsync(placeId, "en");

        result.Should().NotBeNull();
        result!.Id.Should().Be(task.Id);
        result.Status.Should().Be("Running");
    }

    // ---------- ProcessTaskAsync ----------

    [Fact]
    public async Task ProcessTaskAsync_WhenTaskAlreadyDone_ReturnsEarlyWithoutProcessing()
    {
        var task = EntityFactory.CreateAudioTask(Guid.NewGuid(), "vi", status: AudioTaskStatus.Done);
        _audioTaskRepository.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var sut = CreateSut();
        await sut.ProcessTaskAsync(task.Id);

        await _placeRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTaskAsync_WhenTaskMissing_ThrowsNotFound()
    {
        _audioTaskRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((AudioGenerationTask?)null);

        var sut = CreateSut();
        var act = async () => await sut.ProcessTaskAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ProcessTaskAsync_WhenPlaceNoLongerExists_MarksTaskFailed()
    {
        var task = EntityFactory.CreateAudioTask(Guid.NewGuid(), "vi", status: AudioTaskStatus.Pending);
        _audioTaskRepository.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _placeRepository.GetByIdAsync(task.PlaceId, Arg.Any<CancellationToken>()).Returns((Place?)null);

        var sut = CreateSut();
        await sut.ProcessTaskAsync(task.Id);

        task.Status.Should().Be(AudioTaskStatus.Failed);
        task.Error.Should().Be("Place no longer exists.");
    }

    [Fact]
    public async Task ProcessTaskAsync_WhenSuccessful_CreatesNewReadyAudioAssetAndMarksTaskDone()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung để tổng hợp giọng nói.");
        var task = EntityFactory.CreateAudioTask(place.Id, "vi", status: AudioTaskStatus.Pending);

        _audioTaskRepository.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns((AudioAsset?)null);

        var sut = CreateSut();
        await sut.ProcessTaskAsync(task.Id);

        task.Status.Should().Be(AudioTaskStatus.Done);
        task.Error.Should().BeNull();
        task.CompletedAt.Should().NotBeNull();

        await _audioAssetRepository.Received(1).AddAsync(
            Arg.Is<AudioAsset>(a => a.PlaceId == place.Id && a.Lang == "vi" && a.Status == AudioAssetStatus.Ready),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTaskAsync_WhenSuccessful_UpdatesExistingAudioAssetInPlace()
    {
        var place = EntityFactory.CreatePlace(descriptionVi: "Nội dung cập nhật.");
        var task = EntityFactory.CreateAudioTask(place.Id, "vi", status: AudioTaskStatus.Pending);
        var existingAsset = EntityFactory.CreateAudioAsset(place.Id, "vi", status: AudioAssetStatus.Stale, contentHash: "old");

        _audioTaskRepository.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);
        _audioAssetRepository.GetAsync(place.Id, "vi", Arg.Any<CancellationToken>()).Returns(existingAsset);

        var sut = CreateSut();
        await sut.ProcessTaskAsync(task.Id);

        existingAsset.Status.Should().Be(AudioAssetStatus.Ready);
        existingAsset.ContentHash.Should().Be(task.ContentHash);
        await _audioAssetRepository.DidNotReceive().AddAsync(Arg.Any<AudioAsset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTaskAsync_WhenTtsProviderNotConfigured_IncrementsAttemptsAndRequeuesWhenUnderMaxRetries()
    {
        _ttsProvider = FakeProviders.CreateTextToSpeechProvider(isConfigured: false);

        var place = EntityFactory.CreatePlace();
        var task = EntityFactory.CreateAudioTask(place.Id, "vi", status: AudioTaskStatus.Pending);
        task.Attempts = 0;

        _audioTaskRepository.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var sut = CreateSut();
        await sut.ProcessTaskAsync(task.Id);

        task.Attempts.Should().Be(1);
        task.Status.Should().Be(AudioTaskStatus.Pending, "max retries (3) has not been reached yet");
        task.Error.Should().NotBeNullOrEmpty();
        await _audioTaskQueue.Received(1).EnqueueAsync(task.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTaskAsync_WhenAttemptsReachMaxRetries_MarksFailedAndDoesNotRequeue()
    {
        _ttsProvider = FakeProviders.CreateTextToSpeechProvider(isConfigured: false);

        var place = EntityFactory.CreatePlace();
        var task = EntityFactory.CreateAudioTask(place.Id, "vi", status: AudioTaskStatus.Pending);
        task.Attempts = _options.MaxRetries - 1;

        _audioTaskRepository.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _placeRepository.GetByIdAsync(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var sut = CreateSut();
        await sut.ProcessTaskAsync(task.Id);

        task.Attempts.Should().Be(_options.MaxRetries);
        task.Status.Should().Be(AudioTaskStatus.Failed);
        await _audioTaskQueue.DidNotReceive().EnqueueAsync(task.Id, Arg.Any<CancellationToken>());
    }

    // ---------- RecoverPendingTasksAsync ----------

    [Fact]
    public async Task RecoverPendingTasksAsync_WhenNoPendingWork_DoesNothing()
    {
        _audioTaskRepository.GetPendingOrRunningAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<AudioGenerationTask>)[]);

        var sut = CreateSut();
        await sut.RecoverPendingTasksAsync();

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _audioTaskQueue.DidNotReceive().EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverPendingTasksAsync_ResetsRunningTasksToPendingAndRequeuesEverything()
    {
        var runningTask = EntityFactory.CreateAudioTask(Guid.NewGuid(), "vi", status: AudioTaskStatus.Running);
        var pendingTask = EntityFactory.CreateAudioTask(Guid.NewGuid(), "en", status: AudioTaskStatus.Pending);

        _audioTaskRepository.GetPendingOrRunningAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<AudioGenerationTask>)[runningTask, pendingTask]);

        var sut = CreateSut();
        await sut.RecoverPendingTasksAsync();

        runningTask.Status.Should().Be(AudioTaskStatus.Pending, "a task stuck Running means a worker crashed mid-flight");
        pendingTask.Status.Should().Be(AudioTaskStatus.Pending);

        await _audioTaskQueue.Received(1).EnqueueAsync(runningTask.Id, Arg.Any<CancellationToken>());
        await _audioTaskQueue.Received(1).EnqueueAsync(pendingTask.Id, Arg.Any<CancellationToken>());
    }
}

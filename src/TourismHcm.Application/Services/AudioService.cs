using Mapster;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Options;
using TourismHcm.Contracts.Audio;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Exceptions;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.Application.Services;

public sealed class AudioService : IAudioService
{
    private readonly IPlaceRepository _placeRepository;
    private readonly ITranslationRepository _translationRepository;
    private readonly IAudioAssetRepository _audioAssetRepository;
    private readonly IAudioTaskRepository _audioTaskRepository;
    private readonly ITranslationProvider _translationProvider;
    private readonly ITextToSpeechProvider _ttsProvider;
    private readonly IAudioStorage _audioStorage;
    private readonly IAudioTaskQueue _audioTaskQueue;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AudioOptions _options;
    private readonly ILogger<AudioService> _logger;

    public AudioService(
        IPlaceRepository placeRepository,
        ITranslationRepository translationRepository,
        IAudioAssetRepository audioAssetRepository,
        IAudioTaskRepository audioTaskRepository,
        ITranslationProvider translationProvider,
        ITextToSpeechProvider ttsProvider,
        IAudioStorage audioStorage,
        IAudioTaskQueue audioTaskQueue,
        IUnitOfWork unitOfWork,
        IOptions<AudioOptions> options,
        ILogger<AudioService> logger)
    {
        _placeRepository = placeRepository;
        _translationRepository = translationRepository;
        _audioAssetRepository = audioAssetRepository;
        _audioTaskRepository = audioTaskRepository;
        _translationProvider = translationProvider;
        _ttsProvider = ttsProvider;
        _audioStorage = audioStorage;
        _audioTaskQueue = audioTaskQueue;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureTranslationAsync(Guid placeId, string lang, CancellationToken ct = default)
    {
        var normalizedLang = AppLanguages.Normalize(lang);
        if (string.Equals(normalizedLang, AppLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase))
        {
            // Vietnamese is the source language; it lives directly on the Place entity.
            return;
        }

        var place = await _placeRepository.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        var existing = await _translationRepository.GetAsync(placeId, normalizedLang, ct);

        if (existing is not null && existing.Source == TranslationSource.Manual)
        {
            // Manually curated translations are never overwritten automatically.
            return;
        }

        if (existing is not null && string.Equals(existing.ContentHash, place.ContentHash, StringComparison.Ordinal))
        {
            // Already up to date with the current source text.
            return;
        }

        if (!_translationProvider.IsConfigured)
        {
            throw new ProviderUnavailableException(_translationProvider.ProviderName, "Translation provider is not configured.");
        }

        var translatedText = await _translationProvider.TranslateAsync(
            place.DescriptionVi, AppLanguages.Vietnamese, normalizedLang, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.Text = translatedText;
            existing.Source = TranslationSource.Machine;
            existing.ContentHash = place.ContentHash;
            existing.UpdatedAt = now;
        }
        else
        {
            await _translationRepository.AddAsync(new PlaceTranslation
            {
                Id = Guid.NewGuid(),
                PlaceId = placeId,
                Lang = normalizedLang,
                Text = translatedText,
                Source = TranslationSource.Machine,
                ContentHash = place.ContentHash,
                UpdatedAt = now
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<AudioTaskDto> RequestAudioAsync(Guid placeId, string lang, CancellationToken ct = default)
    {
        var normalizedLang = AppLanguages.Normalize(lang);
        var place = await _placeRepository.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        await EnsureTranslationAsync(placeId, normalizedLang, ct);

        var sourceText = await GetTextAsync(place, normalizedLang, ct);
        if (sourceText.Length > _options.MaxTextLength)
        {
            sourceText = sourceText[.._options.MaxTextLength];
        }

        var contentHash = ContentHash.Compute(sourceText);

        var existingAsset = await _audioAssetRepository.GetAsync(placeId, normalizedLang, ct);
        if (existingAsset is not null
            && existingAsset.Status == AudioAssetStatus.Ready
            && string.Equals(existingAsset.ContentHash, contentHash, StringComparison.Ordinal))
        {
            return new AudioTaskDto
            {
                Id = Guid.Empty,
                PlaceId = placeId,
                Lang = normalizedLang,
                Status = AudioTaskStatus.Done.ToString(),
                CreatedAt = existingAsset.CreatedAt,
                UpdatedAt = existingAsset.UpdatedAt,
                CompletedAt = existingAsset.UpdatedAt
            };
        }

        var existingTask = await _audioTaskRepository.GetAsync(placeId, normalizedLang, ct);
        if (existingTask is not null
            && string.Equals(existingTask.ContentHash, contentHash, StringComparison.Ordinal)
            && existingTask.Status is AudioTaskStatus.Pending or AudioTaskStatus.Running)
        {
            return existingTask.Adapt<AudioTaskDto>();
        }

        var now = DateTimeOffset.UtcNow;
        var task = new AudioGenerationTask
        {
            Id = Guid.NewGuid(),
            PlaceId = placeId,
            Lang = normalizedLang,
            ContentHash = contentHash,
            Status = AudioTaskStatus.Pending,
            Attempts = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _audioTaskRepository.AddAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _audioTaskQueue.EnqueueAsync(task.Id, ct);

        return task.Adapt<AudioTaskDto>();
    }

    public async Task<TtsStreamResult> StreamAsync(Guid placeId, string lang, CancellationToken ct = default)
    {
        var normalizedLang = AppLanguages.Normalize(lang);
        var asset = await _audioAssetRepository.GetAsync(placeId, normalizedLang, ct);

        if (asset is null || asset.Status != AudioAssetStatus.Ready)
        {
            throw new NotFoundException("AudioAsset", $"{placeId}:{normalizedLang}");
        }

        var stream = await _audioStorage.OpenReadAsync(asset.Path, ct)
            ?? throw new NotFoundException("AudioAsset", $"{placeId}:{normalizedLang}");

        return new TtsStreamResult
        {
            Content = stream,
            ContentType = "audio/mpeg",
            DurationSec = asset.DurationSec,
            FromCache = true
        };
    }

    public async Task RegenerateAllLanguagesAsync(Guid placeId, CancellationToken ct = default)
    {
        foreach (var lang in _options.PriorityLanguages)
        {
            try
            {
                await RequestAudioAsync(placeId, lang, ct);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Failed to request audio regeneration for place {PlaceId} lang {Lang}", placeId, lang);
            }
        }
    }

    public async Task<AudioTaskDto?> GetTaskStatusAsync(Guid placeId, string lang, CancellationToken ct = default)
    {
        var normalizedLang = AppLanguages.Normalize(lang);
        var task = await _audioTaskRepository.GetAsync(placeId, normalizedLang, ct);
        return task?.Adapt<AudioTaskDto>();
    }

    public async Task ProcessTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _audioTaskRepository.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException("AudioGenerationTask", taskId);

        if (task.Status == AudioTaskStatus.Done)
        {
            return;
        }

        var place = await _placeRepository.GetByIdAsync(task.PlaceId, ct);
        if (place is null)
        {
            task.Status = AudioTaskStatus.Failed;
            task.Error = "Place no longer exists.";
            task.UpdatedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        task.Status = AudioTaskStatus.Running;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        try
        {
            await EnsureTranslationAsync(task.PlaceId, task.Lang, ct);
            var text = await GetTextAsync(place, task.Lang, ct);

            if (!_ttsProvider.IsConfigured)
            {
                throw new ProviderUnavailableException(_ttsProvider.ProviderName, "Text-to-speech provider is not configured.");
            }

            var audioBytes = await _ttsProvider.SynthesizeAsync(text, task.Lang, ct);
            var path = await _audioStorage.SaveAsync(task.PlaceId, task.Lang, task.ContentHash, audioBytes, ct);
            var durationSec = EstimateDurationSeconds(text);

            var existingAsset = await _audioAssetRepository.GetAsync(task.PlaceId, task.Lang, ct);
            var now = DateTimeOffset.UtcNow;

            if (existingAsset is not null)
            {
                existingAsset.Path = path;
                existingAsset.ContentHash = task.ContentHash;
                existingAsset.DurationSec = durationSec;
                existingAsset.Status = AudioAssetStatus.Ready;
                existingAsset.UpdatedAt = now;
            }
            else
            {
                await _audioAssetRepository.AddAsync(new AudioAsset
                {
                    Id = Guid.NewGuid(),
                    PlaceId = task.PlaceId,
                    Lang = task.Lang,
                    ContentHash = task.ContentHash,
                    Path = path,
                    DurationSec = durationSec,
                    Status = AudioAssetStatus.Ready,
                    CreatedAt = now,
                    UpdatedAt = now
                }, ct);
            }

            task.Status = AudioTaskStatus.Done;
            task.Error = null;
            task.CompletedAt = now;
            task.UpdatedAt = now;

            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            task.Attempts += 1;
            task.Error = ex.Message;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            task.Status = task.Attempts >= _options.MaxRetries ? AudioTaskStatus.Failed : AudioTaskStatus.Pending;

            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogWarning(ex, "Audio generation failed for task {TaskId} (attempt {Attempt})", task.Id, task.Attempts);

            if (task.Status == AudioTaskStatus.Pending)
            {
                await _audioTaskQueue.EnqueueAsync(task.Id, ct);
            }
        }
    }

    public async Task RecoverPendingTasksAsync(CancellationToken ct = default)
    {
        var tasks = await _audioTaskRepository.GetPendingOrRunningAsync(ct);
        if (tasks.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var task in tasks)
        {
            if (task.Status == AudioTaskStatus.Running)
            {
                // A task left Running almost certainly means the previous worker crashed mid-flight.
                task.Status = AudioTaskStatus.Pending;
                task.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var task in tasks)
        {
            await _audioTaskQueue.EnqueueAsync(task.Id, ct);
        }
    }

    private async Task<string> GetTextAsync(Place place, string normalizedLang, CancellationToken ct)
    {
        if (string.Equals(normalizedLang, AppLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase))
        {
            return place.DescriptionVi;
        }

        var translation = await _translationRepository.GetAsync(place.Id, normalizedLang, ct);
        return translation?.Text ?? place.DescriptionVi;
    }

    private static double EstimateDurationSeconds(string text)
    {
        var wordCount = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        // Rough narration pace heuristic (~150 words per minute == 2.5 words/sec).
        const double wordsPerSecond = 2.5;
        return wordCount == 0 ? 0 : Math.Round(wordCount / wordsPerSecond, 1);
    }
}

using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Audio;
using TourismHcm.Contracts.Narration;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audio")]
public sealed class AudioController : ControllerBase
{
    private readonly IAudioService _audioService;
    private readonly ILanguageService _languageService;

    public AudioController(IAudioService audioService, ILanguageService languageService)
    {
        _audioService = audioService;
        _languageService = languageService;
    }

    /// <summary>Lists supported narration languages. Anonymous access allowed.</summary>
    [HttpGet("languages")]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<LanguageInfoDto>> GetLanguages() => Ok(_languageService.GetLanguages());

    /// <summary>
    /// Gets the current audio generation task status for a place+language pair.
    /// The route parameter is the place id (the task is uniquely identified by place+lang).
    /// Anonymous access allowed.
    /// </summary>
    [HttpGet("tasks/{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<AudioTaskDto>> GetTaskStatus(Guid id, [FromQuery] string lang, CancellationToken ct)
    {
        var task = await _audioService.GetTaskStatusAsync(id, lang, ct);
        return task is null ? NotFound() : Ok(task);
    }

    /// <summary>
    /// Streams task status updates as Server-Sent Events until the task reaches a terminal
    /// state (Done/Failed). Anonymous access allowed.
    /// </summary>
    [HttpGet("tasks/{id:guid}/events")]
    [AllowAnonymous]
    public async Task GetTaskEvents(Guid id, [FromQuery] string lang, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        const int maxIterations = 300; // ~5 minutes at 1s polling, as a safety upper bound.
        const int pollDelayMs = 1000;

        for (var i = 0; i < maxIterations && !ct.IsCancellationRequested; i++)
        {
            var task = await _audioService.GetTaskStatusAsync(id, lang, ct);

            if (task is null)
            {
                await WriteEventAsync("error", new { message = "Task not found." }, ct);
                return;
            }

            await WriteEventAsync("status", task, ct);

            if (string.Equals(task.Status, AudioTaskStatus.Done.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(task.Status, AudioTaskStatus.Failed.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(task.Status, AudioTaskStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                await Task.Delay(pollDelayMs, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Requests (or retrieves the existing/queued) audio generation for a place+language pair.
    /// Rate-limited more strictly than general endpoints. Anonymous access allowed.
    /// </summary>
    [HttpPost("tts")]
    [AllowAnonymous]
    [EnableRateLimiting("tts")]
    public async Task<ActionResult<AudioTaskDto>> RequestTts([FromBody] TtsRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Lang))
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                [nameof(request.Lang)] = ["Lang is required."],
            });
        }

        var result = await _audioService.RequestAudioAsync(request.PlaceId, request.Lang, ct);
        return Ok(result);
    }

    /// <summary>Forces regeneration of audio/translations for every priority language. Requires editor or admin role.</summary>
    [HttpPost("regenerate/{placeId:guid}")]
    [Authorize(Policy = "RequireEditor")]
    [EnableRateLimiting("tts")]
    public async Task<IActionResult> Regenerate(Guid placeId, CancellationToken ct)
    {
        await _audioService.RegenerateAllLanguagesAsync(placeId, ct);
        return Accepted();
    }

    [HttpGet("pack-manifest")]
    [AllowAnonymous]
    public async Task<ActionResult<AudioPackManifestDto>> GetPackManifest([FromQuery] string lang, CancellationToken ct)
    {
        return Ok(await _audioService.GetPackManifestAsync(lang, ct));
    }

    [HttpPost("tasks/{taskId:guid}/cancel")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<AudioTaskDto>> CancelTask(Guid taskId, CancellationToken ct)
    {
        return Ok(await _audioService.CancelTaskAsync(taskId, ct));
    }

    [HttpPost("tasks/{taskId:guid}/pause")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<AudioTaskDto>> PauseTask(Guid taskId, CancellationToken ct) =>
        Ok(await _audioService.PauseTaskAsync(taskId, ct));

    [HttpPost("tasks/{taskId:guid}/resume")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<AudioTaskDto>> ResumeTask(Guid taskId, CancellationToken ct) =>
        Ok(await _audioService.ResumeTaskAsync(taskId, ct));

    private async Task WriteEventAsync(string eventName, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        await Response.WriteAsync($"event: {eventName}\n", ct);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}

/// <summary>Request body for <see cref="AudioController.RequestTts"/>: identifies the place and target language only.</summary>
public sealed class TtsRequest
{
    public Guid PlaceId { get; init; }

    public string Lang { get; init; } = string.Empty;
}

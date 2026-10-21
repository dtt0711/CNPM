using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Admin;
using TourismHcm.Contracts.Audio;
using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IAudioService _audioService;
    private readonly ICurrentUser _currentUser;

    public AdminController(IAdminService adminService, IAudioService audioService, ICurrentUser currentUser)
    {
        _adminService = adminService;
        _audioService = audioService;
        _currentUser = currentUser;
    }

    /// <summary>Publishes (approves) a draft/hidden place.</summary>
    [HttpPost("places/{placeId:guid}/approve")]
    public async Task<ActionResult<PlaceDetailDto>> ApprovePlace(Guid placeId, CancellationToken ct)
    {
        var result = await _adminService.ApprovePlaceAsync(placeId, RequireUserId(), ct);
        return Ok(result);
    }

    /// <summary>Hides a published place.</summary>
    [HttpPost("places/{placeId:guid}/hide")]
    public async Task<ActionResult<PlaceDetailDto>> HidePlace(Guid placeId, CancellationToken ct)
    {
        var result = await _adminService.HidePlaceAsync(placeId, RequireUserId(), ct);
        return Ok(result);
    }

    /// <summary>Manually edits/overrides a place translation for a given language.</summary>
    [HttpPut("translations")]
    public async Task<IActionResult> UpdateTranslation([FromBody] UpdateTranslationRequest request, CancellationToken ct)
    {
        await _adminService.UpdateTranslationAsync(RequireUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Forces regeneration of translations/audio for every priority language, with audit logging.</summary>
    [HttpPost("places/{placeId:guid}/regenerate-audio")]
    public async Task<IActionResult> RegenerateAudio(Guid placeId, CancellationToken ct)
    {
        await _adminService.RegenerateAudioAsync(placeId, RequireUserId(), ct);
        return Accepted();
    }

    /// <summary>Searches the audit log.</summary>
    [HttpGet("audit-logs")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLogs(
        [FromQuery] Guid? userId, [FromQuery] string? entityType, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _adminService.GetAuditLogsAsync(userId, entityType, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> ListUsers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        return Ok(await _adminService.ListUsersAsync(page, pageSize, ct));
    }

    [HttpPut("users/{userId:guid}/active")]
    public async Task<IActionResult> SetUserActive(Guid userId, [FromBody] UpdateUserActiveRequest request, CancellationToken ct)
    {
        await _adminService.SetUserActiveAsync(RequireUserId(), userId, request.IsActive, ct);
        return NoContent();
    }

    [HttpPut("users/{userId:guid}/roles")]
    public async Task<IActionResult> AssignUserRoles(
        Guid userId, [FromBody] AssignUserRolesRequest request, CancellationToken ct)
    {
        await _adminService.AssignUserRolesAsync(RequireUserId(), userId, request, ct);
        return NoContent();
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<AdminRoleDto>>> ListRoles(CancellationToken ct)
    {
        return Ok(await _adminService.ListRolesAsync(ct));
    }

    [HttpPut("roles/{roleId:guid}/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(
        Guid roleId, [FromBody] UpdateRolePermissionsRequest request, CancellationToken ct)
    {
        await _adminService.UpdateRolePermissionsAsync(RequireUserId(), roleId, request, ct);
        return NoContent();
    }

    /// <summary>Lists active/recent audio generation tasks (AudioTaskManager snapshot).</summary>
    [HttpGet("audio-tasks")]
    public async Task<ActionResult<IReadOnlyList<AudioTaskDto>>> ListAudioTasks(
        [FromQuery] int take = 50, CancellationToken ct = default)
    {
        return Ok(await _audioService.ListRecentTasksAsync(take, ct));
    }

    /// <summary>SSE fan-out of audio task status for the Admin progress UI.</summary>
    [HttpGet("audio-tasks/stream")]
    public async Task StreamAudioTasks(CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        const int maxIterations = 1800; // ~30 minutes at 1s
        for (var i = 0; i < maxIterations && !ct.IsCancellationRequested; i++)
        {
            var tasks = await _audioService.ListRecentTasksAsync(80, ct);
            var json = JsonSerializer.Serialize(tasks);
            await Response.WriteAsync($"event: tasks\ndata: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);

            try
            {
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [HttpPost("audio-tasks/{taskId:guid}/pause")]
    public async Task<ActionResult<AudioTaskDto>> PauseAudioTask(Guid taskId, CancellationToken ct) =>
        Ok(await _audioService.PauseTaskAsync(taskId, ct));

    [HttpPost("audio-tasks/{taskId:guid}/resume")]
    public async Task<ActionResult<AudioTaskDto>> ResumeAudioTask(Guid taskId, CancellationToken ct) =>
        Ok(await _audioService.ResumeTaskAsync(taskId, ct));

    [HttpPost("audio-tasks/{taskId:guid}/cancel")]
    public async Task<ActionResult<AudioTaskDto>> CancelAudioTask(Guid taskId, CancellationToken ct) =>
        Ok(await _audioService.CancelTaskAsync(taskId, ct));

    private Guid RequireUserId() => _currentUser.UserId ?? throw new UnauthorizedDomainException();
}

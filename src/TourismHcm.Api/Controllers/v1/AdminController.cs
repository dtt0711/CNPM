using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Admin;
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
    private readonly ICurrentUser _currentUser;

    public AdminController(IAdminService adminService, ICurrentUser currentUser)
    {
        _adminService = adminService;
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

    private Guid RequireUserId() => _currentUser.UserId ?? throw new UnauthorizedDomainException();
}

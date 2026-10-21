using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Api.Common;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Narration;
using TourismHcm.Contracts.Places;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/places")]
public sealed class PlacesController : ControllerBase
{
    private readonly IPlaceService _placeService;
    private readonly IGeofenceService _geofenceService;
    private readonly INarrationService _narrationService;
    private readonly IValidator<CreatePlaceRequest> _createValidator;
    private readonly IValidator<UpdatePlaceRequest> _updateValidator;
    private readonly IValidator<TriggerRequest> _triggerValidator;

    public PlacesController(
        IPlaceService placeService,
        IGeofenceService geofenceService,
        INarrationService narrationService,
        IValidator<CreatePlaceRequest> createValidator,
        IValidator<UpdatePlaceRequest> updateValidator,
        IValidator<TriggerRequest> triggerValidator)
    {
        _placeService = placeService;
        _geofenceService = geofenceService;
        _narrationService = narrationService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _triggerValidator = triggerValidator;
    }

    /// <summary>Searches/lists places. Anonymous access allowed.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<PlaceDto>>> Search([FromQuery] PlaceQueryRequest query, CancellationToken ct)
    {
        var result = await _placeService.SearchAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets places near a given coordinate. Pass <c>radiusMeters=0</c> for nearest without a distance cap.
    /// Anonymous access allowed.
    /// </summary>
    [HttpGet("nearby")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<NearbyPlaceDto>>> Nearby(
        [FromQuery] double lat, [FromQuery] double lng, [FromQuery] double radiusMeters = 500, [FromQuery] int? limit = 20,
        CancellationToken ct = default)
    {
        var result = await _placeService.GetNearbyAsync(lat, lng, radiusMeters, limit, ct);
        return Ok(result);
    }

    /// <summary>Evaluates a geofence trigger (query). Anonymous access allowed. Client id from X-Client-Id or user id.</summary>
    [HttpGet("trigger")]
    [AllowAnonymous]
    public async Task<ActionResult<TriggerResponse>> TriggerGet(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] string lang = "vi",
        CancellationToken ct = default)
    {
        var request = BuildTriggerRequest(lat, lng, lang);
        await _triggerValidator.ValidateAndThrowDomainAsync(request, ct);

        var result = await _geofenceService.TriggerAsync(request, ct);
        return result is null ? NoContent() : Ok(result);
    }

    /// <summary>Evaluates a geofence trigger (body). Anonymous access allowed.</summary>
    [HttpPost("trigger")]
    [AllowAnonymous]
    public async Task<ActionResult<TriggerResponse>> TriggerPost([FromBody] TriggerRequest request, CancellationToken ct)
    {
        var enriched = request with { ClientId = ResolveClientId(request.ClientId) };
        await _triggerValidator.ValidateAndThrowDomainAsync(enriched, ct);

        var result = await _geofenceService.TriggerAsync(enriched, ct);
        return result is null ? NoContent() : Ok(result);
    }

    private TriggerRequest BuildTriggerRequest(double lat, double lng, string lang) =>
        new()
        {
            Latitude = lat,
            Longitude = lng,
            Lang = lang,
            ClientId = ResolveClientId(null)
        };

    private string ResolveClientId(string? fromBody)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(sub))
            {
                return sub;
            }
        }

        if (!string.IsNullOrWhiteSpace(fromBody))
        {
            return fromBody;
        }

        if (Request.Headers.TryGetValue("X-Client-Id", out var header) && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return "anonymous";
    }

    /// <summary>Gets narration content (with language fallback) for a place. Anonymous access allowed.</summary>
    [HttpGet("{id:guid}/narration")]
    [AllowAnonymous]
    public async Task<ActionResult<NarrationResponse>> GetNarration(Guid id, [FromQuery] string lang = "vi", CancellationToken ct = default)
    {
        var result = await _narrationService.GetNarrationAsync(id, lang, ct);
        return Ok(result);
    }

    /// <summary>Gets full detail for a single place. Anonymous access allowed.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PlaceDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _placeService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Creates a new place. Requires editor or admin role.</summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<PlaceDetailDto>> Create([FromBody] CreatePlaceRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowDomainAsync(request, ct);

        var result = await _placeService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    /// <summary>Updates an existing place. Requires editor or admin role.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<PlaceDetailDto>> Update(Guid id, [FromBody] UpdatePlaceRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowDomainAsync(request, ct);

        var result = await _placeService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>Deletes a place. Requires admin role.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _placeService.DeleteAsync(id, ct);
        return NoContent();
    }
}

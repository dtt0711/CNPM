using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Localization;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/localizations")]
[AllowAnonymous]
public sealed class LocalizationsController : ControllerBase
{
    private readonly ILocalizationService _localizationService;

    public LocalizationsController(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    [HttpPost("on-demand")]
    [EnableRateLimiting("tts")]
    public async Task<ActionResult<OnDemandLocalizationResult>> OnDemand(
        [FromBody] OnDemandRequest request,
        CancellationToken ct)
    {
        var result = await _localizationService.OnDemandAsync(request.PlaceId, request.Lang, ct);
        if (result.RetryAfterSeconds is int seconds)
        {
            Response.Headers.RetryAfter = seconds.ToString();
        }

        return Ok(result);
    }

    [HttpPost("warmup")]
    [EnableRateLimiting("tts")]
    public async Task<ActionResult<WarmupResult>> Warmup([FromBody] WarmupRequest request, CancellationToken ct)
    {
        return Ok(await _localizationService.WarmupAsync(request.Lang, request.PlaceIds, ct));
    }

    [HttpPost("prepare-hotset")]
    public async Task<ActionResult<HotsetResult>> PrepareHotset(
        [FromBody] HotsetRequest request,
        CancellationToken ct)
    {
        return Ok(await _localizationService.PrepareHotsetAsync(request.Lat, request.Lng, request.Lang, ct));
    }
}

public sealed class OnDemandRequest
{
    public Guid PlaceId { get; init; }

    public string Lang { get; init; } = "en";
}

public sealed class WarmupRequest
{
    public string Lang { get; init; } = "en";

    public List<Guid>? PlaceIds { get; init; }
}

public sealed class HotsetRequest
{
    public double Lat { get; init; }

    public double Lng { get; init; }

    public string Lang { get; init; } = "en";
}

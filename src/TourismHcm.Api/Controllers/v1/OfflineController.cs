using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Offline;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/offline")]
public sealed class OfflineController : ControllerBase
{
    private readonly IOfflinePackService _offlinePackService;

    public OfflineController(IOfflinePackService offlinePackService)
    {
        _offlinePackService = offlinePackService;
    }

    /// <summary>Builds an offline content manifest (places, text, available audio URLs). Anonymous access allowed.</summary>
    [HttpGet("pack")]
    [AllowAnonymous]
    public async Task<ActionResult<OfflinePackManifest>> GetPack(
        [FromQuery] string? lang,
        [FromQuery] string[]? langs,
        CancellationToken ct)
    {
        IEnumerable<string>? requested = langs;
        if ((requested is null || !requested.Any()) && !string.IsNullOrWhiteSpace(lang))
        {
            requested = [lang];
        }

        var manifest = await _offlinePackService.BuildManifestAsync(requested, ct);
        return Ok(manifest);
    }
}

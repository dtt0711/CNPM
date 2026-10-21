using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Maps;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/maps")]
[AllowAnonymous]
public sealed class MapsController : ControllerBase
{
    private readonly IMapPackService _mapPackService;

    public MapsController(IMapPackService mapPackService)
    {
        _mapPackService = mapPackService;
    }

    [HttpGet("offline/options")]
    public async Task<ActionResult<MapOfflineOptionsDto>> GetOfflineOptions(CancellationToken ct)
    {
        return Ok(await _mapPackService.GetOfflineOptionsAsync(ct));
    }

    [HttpGet("offline/manifest/{packId}")]
    public async Task<ActionResult<MapPackManifestDto>> GetOfflineManifest(string packId, CancellationToken ct)
    {
        return Ok(await _mapPackService.GetOfflineManifestAsync(packId, ct));
    }

    [HttpGet("packs")]
    public async Task<ActionResult<IReadOnlyList<MapPackManifestDto>>> ListPacks(CancellationToken ct)
    {
        return Ok(await _mapPackService.ListPacksAsync(ct));
    }
}

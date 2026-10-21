using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.UiBundles;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ui-bundles")]
[AllowAnonymous]
public sealed class UiBundlesController : ControllerBase
{
    private readonly IUiBundleService _uiBundleService;

    public UiBundlesController(IUiBundleService uiBundleService)
    {
        _uiBundleService = uiBundleService;
    }

    [HttpGet("{locale}")]
    public async Task<ActionResult<UiBundleDto>> GetBundle(string locale, CancellationToken ct)
    {
        return Ok(await _uiBundleService.GetBundleAsync(locale, ct));
    }
}

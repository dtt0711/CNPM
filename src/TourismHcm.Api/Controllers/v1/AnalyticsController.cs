using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Analytics;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpPost("collect")]
    [AllowAnonymous]
    public async Task<IActionResult> Collect([FromBody] AnalyticsCollectRequest request, CancellationToken ct)
    {
        await _analyticsService.CollectAsync(request, ct);
        return Accepted();
    }

    [HttpPost("presence")]
    [AllowAnonymous]
    public async Task<IActionResult> Presence([FromBody] AnalyticsPresenceRequest request, CancellationToken ct)
    {
        await _analyticsService.RecordPresenceAsync(request.DeviceId, ct);
        return NoContent();
    }

    [HttpGet("admin/summary")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<AnalyticsAdminSummaryDto>> GetAdminSummary(CancellationToken ct)
    {
        return Ok(await _analyticsService.GetAdminSummaryAsync(ct));
    }

    [HttpGet("export")]
    [Authorize(Policy = "Permission:analytics.export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var csv = await _analyticsService.ExportCsvAsync(ct);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "analytics-export.csv");
    }
}

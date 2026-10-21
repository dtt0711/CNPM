using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Ai;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ai")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AiController : ControllerBase
{
    private readonly IAiAdvisorService _aiAdvisorService;
    private readonly ICurrentUser _currentUser;

    public AiController(IAiAdvisorService aiAdvisorService, ICurrentUser currentUser)
    {
        _aiAdvisorService = aiAdvisorService;
        _currentUser = currentUser;
    }

    [HttpGet("usage")]
    public async Task<ActionResult<AiUsageDto>> GetUsage(CancellationToken ct)
    {
        var result = await _aiAdvisorService.GetUsageAsync(RequireUserId(), GetRoles(), ct);
        return Ok(result);
    }

    [HttpPost("enhance-description")]
    public async Task<ActionResult<EnhanceDescriptionResponse>> EnhanceDescription(
        [FromBody] EnhanceDescriptionRequest request, CancellationToken ct)
    {
        var result = await _aiAdvisorService.EnhanceDescriptionAsync(RequireUserId(), GetRoles(), request, ct);
        return Ok(result);
    }

    private Guid RequireUserId() => _currentUser.UserId ?? throw new UnauthorizedDomainException();

    private IReadOnlyList<string> GetRoles() =>
        User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
}

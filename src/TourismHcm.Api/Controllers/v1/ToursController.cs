using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Api.Common;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Tours;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tours")]
[Authorize]
public sealed class ToursController : ControllerBase
{
    private readonly ITourService _tourService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateTourRequest> _createValidator;

    public ToursController(ITourService tourService, ICurrentUser currentUser, IValidator<CreateTourRequest> createValidator)
    {
        _tourService = tourService;
        _currentUser = currentUser;
        _createValidator = createValidator;
    }

    /// <summary>Lists the caller's saved tours.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TourDto>>> GetMine(CancellationToken ct)
    {
        var result = await _tourService.GetByUserAsync(RequireUserId(), ct);
        return Ok(result);
    }

    /// <summary>Gets a single tour owned by the caller.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TourDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _tourService.GetByIdAsync(RequireUserId(), id, ct);
        return Ok(result);
    }

    /// <summary>Creates a new tour from a set of places, auto-ordered by nearest-neighbor.</summary>
    [HttpPost]
    public async Task<ActionResult<TourDto>> Create([FromBody] CreateTourRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowDomainAsync(request, ct);

        var result = await _tourService.CreateAsync(RequireUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    /// <summary>Deletes a tour owned by the caller.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _tourService.DeleteAsync(RequireUserId(), id, ct);
        return NoContent();
    }

    private Guid RequireUserId() => _currentUser.UserId ?? throw new UnauthorizedDomainException();
}

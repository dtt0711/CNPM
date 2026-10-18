using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Api.Common;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Reviews;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateReviewRequest> _createValidator;

    public ReviewsController(
        IReviewService reviewService, ICurrentUser currentUser, IValidator<CreateReviewRequest> createValidator)
    {
        _reviewService = reviewService;
        _currentUser = currentUser;
        _createValidator = createValidator;
    }

    /// <summary>Lists reviews for a place. Anonymous access allowed.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetByPlace(
        [FromQuery] Guid placeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _reviewService.GetByPlaceAsync(placeId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Creates or updates the caller's review for a place. Requires authentication.</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> CreateOrUpdate([FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowDomainAsync(request, ct);

        var userId = RequireUserId();
        var result = await _reviewService.CreateOrUpdateAsync(userId, request, ct);
        return Ok(result);
    }

    /// <summary>Deletes a review. Owners can delete their own review; admins can delete any review.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = RequireUserId();
        var isAdmin = _currentUser.IsInRole(AppRoles.Admin);

        await _reviewService.DeleteAsync(userId, id, isAdmin, ct);
        return NoContent();
    }

    private Guid RequireUserId() => _currentUser.UserId ?? throw new UnauthorizedDomainException();
}

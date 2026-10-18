using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Favorites;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/favorites")]
[Authorize]
public sealed class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favoriteService;
    private readonly ICurrentUser _currentUser;

    public FavoritesController(IFavoriteService favoriteService, ICurrentUser currentUser)
    {
        _favoriteService = favoriteService;
        _currentUser = currentUser;
    }

    /// <summary>Lists the caller's favorite places.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FavoriteDto>>> GetMine(CancellationToken ct)
    {
        var result = await _favoriteService.GetByUserAsync(RequireUserId(), ct);
        return Ok(result);
    }

    /// <summary>Adds a place to the caller's favorites.</summary>
    [HttpPut("{placeId:guid}")]
    public async Task<IActionResult> Add(Guid placeId, CancellationToken ct)
    {
        await _favoriteService.AddAsync(RequireUserId(), placeId, ct);
        return NoContent();
    }

    /// <summary>Removes a place from the caller's favorites.</summary>
    [HttpDelete("{placeId:guid}")]
    public async Task<IActionResult> Remove(Guid placeId, CancellationToken ct)
    {
        await _favoriteService.RemoveAsync(RequireUserId(), placeId, ct);
        return NoContent();
    }

    private Guid RequireUserId() => _currentUser.UserId ?? throw new UnauthorizedDomainException();
}

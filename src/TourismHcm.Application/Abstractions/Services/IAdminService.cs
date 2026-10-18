using TourismHcm.Contracts.Admin;
using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Places;

namespace TourismHcm.Application.Abstractions.Services;

public interface IAdminService
{
    Task<PlaceDetailDto> ApprovePlaceAsync(Guid placeId, Guid adminUserId, CancellationToken ct = default);

    Task<PlaceDetailDto> HidePlaceAsync(Guid placeId, Guid adminUserId, CancellationToken ct = default);

    Task UpdateTranslationAsync(Guid adminUserId, UpdateTranslationRequest request, CancellationToken ct = default);

    Task RegenerateAudioAsync(Guid placeId, Guid adminUserId, CancellationToken ct = default);

    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        Guid? userId, string? entityType, int page, int pageSize, CancellationToken ct = default);
}

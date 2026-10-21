using System.Text.Json;
using Mapster;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Admin;
using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Exceptions;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.Application.Services;

public sealed class AdminService : IAdminService
{
    private readonly IPlaceRepository _placeRepository;
    private readonly ITranslationRepository _translationRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IPlatformRepository _platformRepository;
    private readonly IAudioService _audioService;
    private readonly IPlaceService _placeService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminService(
        IPlaceRepository placeRepository,
        ITranslationRepository translationRepository,
        IAuditLogRepository auditLogRepository,
        IPlatformRepository platformRepository,
        IAudioService audioService,
        IPlaceService placeService,
        IUnitOfWork unitOfWork)
    {
        _placeRepository = placeRepository;
        _translationRepository = translationRepository;
        _auditLogRepository = auditLogRepository;
        _platformRepository = platformRepository;
        _audioService = audioService;
        _placeService = placeService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlaceDetailDto> ApprovePlaceAsync(Guid placeId, Guid adminUserId, CancellationToken ct = default)
    {
        var place = await _placeRepository.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        place.Status = PlaceStatus.Published;
        place.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        await WriteAuditLogAsync(adminUserId, "ApprovePlace", "Place", placeId.ToString(), null, ct);

        return await _placeService.GetByIdAsync(placeId, ct);
    }

    public async Task<PlaceDetailDto> HidePlaceAsync(Guid placeId, Guid adminUserId, CancellationToken ct = default)
    {
        var place = await _placeRepository.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        place.Status = PlaceStatus.Hidden;
        place.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        await WriteAuditLogAsync(adminUserId, "HidePlace", "Place", placeId.ToString(), null, ct);

        return await _placeService.GetByIdAsync(placeId, ct);
    }

    public async Task UpdateTranslationAsync(Guid adminUserId, UpdateTranslationRequest request, CancellationToken ct = default)
    {
        if (!AppLanguages.IsSupported(request.Lang))
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                [nameof(request.Lang)] = [$"Unsupported language '{request.Lang}'."]
            });
        }

        var place = await _placeRepository.GetByIdAsync(request.PlaceId, ct)
            ?? throw new NotFoundException("Place", request.PlaceId);

        var normalizedLang = AppLanguages.Normalize(request.Lang);
        var now = DateTimeOffset.UtcNow;

        if (string.Equals(normalizedLang, AppLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase))
        {
            place.DescriptionVi = request.Text.Trim();
            place.ContentHash = ContentHash.Compute(place.DescriptionVi);
            place.UpdatedAt = now;
        }
        else
        {
            var existing = await _translationRepository.GetAsync(request.PlaceId, normalizedLang, ct);
            if (existing is not null)
            {
                existing.Text = request.Text.Trim();
                existing.Source = TranslationSource.Manual;
                existing.ContentHash = ContentHash.Compute(request.Text.Trim());
                existing.UpdatedAt = now;
            }
            else
            {
                await _translationRepository.AddAsync(new PlaceTranslation
                {
                    Id = Guid.NewGuid(),
                    PlaceId = request.PlaceId,
                    Lang = normalizedLang,
                    Text = request.Text.Trim(),
                    Source = TranslationSource.Manual,
                    ContentHash = ContentHash.Compute(request.Text.Trim()),
                    UpdatedAt = now
                }, ct);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        await WriteAuditLogAsync(
            adminUserId, "UpdateTranslation", "PlaceTranslation", $"{request.PlaceId}:{normalizedLang}", request.Text, ct);
    }

    public async Task RegenerateAudioAsync(Guid placeId, Guid adminUserId, CancellationToken ct = default)
    {
        _ = await _placeRepository.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        await _audioService.RegenerateAllLanguagesAsync(placeId, ct);

        await WriteAuditLogAsync(adminUserId, "RegenerateAudio", "Place", placeId.ToString(), null, ct);
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        Guid? userId, string? entityType, int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = page <= 0 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

        var (items, totalCount) = await _auditLogRepository.SearchAsync(userId, entityType, safePage, safePageSize, ct);
        var dtos = items.Adapt<List<AuditLogDto>>();

        return PagedResult<AuditLogDto>.Create(dtos, safePage, safePageSize, totalCount);
    }

    public async Task<PagedResult<AdminUserDto>> ListUsersAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = page <= 0 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;
        var (items, total) = await _platformRepository.ListUsersAsync(safePage, safePageSize, ct);
        var dtos = items.Select(u => new AdminUserDto
        {
            Id = u.Id,
            Email = u.Email,
            DisplayName = u.DisplayName,
            IsActive = u.IsActive,
            Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
            CreatedAt = u.CreatedAt
        }).ToList();

        return PagedResult<AdminUserDto>.Create(dtos, safePage, safePageSize, total);
    }

    public async Task SetUserActiveAsync(Guid adminUserId, Guid userId, bool isActive, CancellationToken ct = default)
    {
        _ = await _platformRepository.GetUserWithRolesAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        await _platformRepository.SetUserActiveAsync(userId, isActive, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await WriteAuditLogAsync(adminUserId, "SetUserActive", "User", userId.ToString(), isActive.ToString(), ct);
    }

    public async Task AssignUserRolesAsync(
        Guid adminUserId, Guid userId, AssignUserRolesRequest request, CancellationToken ct = default)
    {
        _ = await _platformRepository.GetUserWithRolesAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        await _platformRepository.AssignUserRolesAsync(userId, request.Roles.ToList(), ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await WriteAuditLogAsync(adminUserId, "AssignUserRoles", "User", userId.ToString(), string.Join(',', request.Roles), ct);
    }

    public async Task<IReadOnlyList<AdminRoleDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var roles = await _platformRepository.GetAllRolesAsync(ct);
        return roles.Select(r => new AdminRoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Priority = r.Priority,
            Permissions = PermissionService.ParsePermissionsJson(r.PermissionsJson, r.Name).ToList()
        }).ToList();
    }

    public async Task UpdateRolePermissionsAsync(
        Guid adminUserId, Guid roleId, UpdateRolePermissionsRequest request, CancellationToken ct = default)
    {
        _ = await _platformRepository.GetRoleByIdAsync(roleId, ct)
            ?? throw new NotFoundException("Role", roleId);

        var json = JsonSerializer.Serialize(request.Permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        await _platformRepository.UpdateRolePermissionsAsync(roleId, json, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await WriteAuditLogAsync(adminUserId, "UpdateRolePermissions", "Role", roleId.ToString(), json, ct);
    }

    private async Task WriteAuditLogAsync(
        Guid adminUserId, string action, string entityType, string entityId, string? details, CancellationToken ct)
    {
        await _auditLogRepository.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }
}

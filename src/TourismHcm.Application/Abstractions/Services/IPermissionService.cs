namespace TourismHcm.Application.Abstractions.Services;

public interface IPermissionService
{
    IReadOnlyList<string> ResolvePermissions(IEnumerable<string> roleNames, IEnumerable<string?> permissionsJsonFromRoles);

    Task<IReadOnlyList<string>> ResolvePermissionsForUserAsync(Guid userId, CancellationToken ct = default);
}

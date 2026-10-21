using System.Text.Json;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Application.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly IUserRepository _userRepository;

    public PermissionService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public IReadOnlyList<string> ResolvePermissions(
        IEnumerable<string> roleNames, IEnumerable<string?> permissionsJsonFromRoles)
    {
        var roles = roleNames.ToList();
        var jsonList = permissionsJsonFromRoles.ToList();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < roles.Count; i++)
        {
            var roleName = roles[i];
            var json = i < jsonList.Count ? jsonList[i] : null;
            foreach (var permission in ParsePermissionsJson(json, roleName))
            {
                set.Add(permission);
            }
        }

        return set.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<string>> ResolvePermissionsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var json = user.UserRoles.Select(ur => ur.Role.PermissionsJson).ToList();
        return ResolvePermissions(roles, json);
    }

    public static IEnumerable<string> ParsePermissionsJson(string? permissionsJson, string roleName)
    {
        if (!string.IsNullOrWhiteSpace(permissionsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<string[]>(permissionsJson);
                if (parsed is { Length: > 0 })
                {
                    return parsed.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim());
                }
            }
            catch (JsonException)
            {
                // Fall through to role defaults.
            }
        }

        return AppPermissions.DefaultPermissionsForRole(roleName);
    }
}

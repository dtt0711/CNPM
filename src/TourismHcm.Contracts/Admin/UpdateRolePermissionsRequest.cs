namespace TourismHcm.Contracts.Admin;

public sealed class UpdateRolePermissionsRequest
{
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

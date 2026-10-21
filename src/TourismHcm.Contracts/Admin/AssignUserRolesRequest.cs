namespace TourismHcm.Contracts.Admin;

public sealed class AssignUserRolesRequest
{
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

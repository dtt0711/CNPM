namespace TourismHcm.Contracts.Admin;

public sealed class AdminRoleDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Priority { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

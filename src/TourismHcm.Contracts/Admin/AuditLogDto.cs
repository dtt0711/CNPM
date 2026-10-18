namespace TourismHcm.Contracts.Admin;

public sealed class AuditLogDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? Details { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

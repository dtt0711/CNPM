namespace TourismHcm.Contracts.Audio;

public sealed class AudioTaskDto
{
    public Guid Id { get; init; }
    public Guid PlaceId { get; init; }
    public string Lang { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Attempts { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

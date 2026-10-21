using TourismHcm.Domain.Enums;

namespace TourismHcm.Domain.Entities;

public class OwnerRegistration
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public OwnerRegistrationStatus Status { get; set; } = OwnerRegistrationStatus.Pending;
    public string NationalIdEncrypted { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public User User { get; set; } = null!;
}

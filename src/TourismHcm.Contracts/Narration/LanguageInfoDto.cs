namespace TourismHcm.Contracts.Narration;

public sealed class LanguageInfoDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? VoiceId { get; init; }
    public bool IsPriority { get; init; }
}

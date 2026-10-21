namespace TourismHcm.Contracts.UiBundles;

public sealed class UiBundleDto
{
    public string Locale { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = "{}";
    public bool IsFallback { get; init; }
}

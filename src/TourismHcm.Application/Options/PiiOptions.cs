namespace TourismHcm.Application.Options;

public sealed class PiiOptions
{
    public const string SectionName = "Pii";

    /// <summary>Base64-encoded 32-byte key for AES-GCM. Generated if empty in Development.</summary>
    public string EncryptionKey { get; set; } = string.Empty;

    public int RedactionDays { get; set; } = 180;
}

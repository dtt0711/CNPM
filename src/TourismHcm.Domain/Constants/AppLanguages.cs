namespace TourismHcm.Domain.Constants;

public static class AppLanguages
{
    public const string Vietnamese = "vi";
    public const string English = "en";
    public const string Japanese = "ja";
    public const string Korean = "ko";
    public const string Chinese = "zh";

    public static readonly string[] Priority = [Vietnamese, English, Japanese, Korean, Chinese];
    public static readonly string[] Supported = Priority;

    public static bool IsSupported(string lang) =>
        Supported.Contains(lang, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string lang) => lang.Trim().ToLowerInvariant() switch
    {
        "zh" or "zh-cn" or "zh_cn" => Chinese,
        var l => l
    };
}

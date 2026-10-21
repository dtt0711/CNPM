namespace TourismHcm.Domain.Constants;

public static class AppRoles
{
    public const string SuperAdmin = "super_admin";
    public const string Admin = "admin";
    public const string User = "user";

    /// <summary>
    /// Obsolete alias for <see cref="Admin"/>; legacy JWT/seed data may still use the <c>editor</c> role name.
    /// </summary>
    [Obsolete("Use Admin instead.")]
    public const string Editor = Admin;

    public const string EditorLegacyName = "editor";

    public static readonly string[] All = [SuperAdmin, Admin, User, EditorLegacyName];
}

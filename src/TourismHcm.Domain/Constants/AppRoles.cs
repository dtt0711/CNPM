namespace TourismHcm.Domain.Constants;

public static class AppRoles
{
    public const string Admin = "admin";
    public const string Editor = "editor";
    public const string User = "user";

    public static readonly string[] All = [Admin, Editor, User];
}

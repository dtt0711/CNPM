namespace TourismHcm.Domain.Constants;

public static class AppPermissions
{
    public const string PlaceRead = "place.read";
    public const string PlaceCreate = "place.create";
    public const string PlaceUpdate = "place.update";
    public const string PlaceDelete = "place.delete";
    public const string PlaceApprove = "place.approve";
    public const string PlaceToggle = "place.toggle";

    public const string TourRead = "tour.read";
    public const string TourCreate = "tour.create";
    public const string TourUpdate = "tour.update";
    public const string TourDelete = "tour.delete";

    public const string UserRead = "user.read";
    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
    public const string UserDelete = "user.delete";

    public const string RoleRead = "role.read";
    public const string RoleCreate = "role.create";
    public const string RoleUpdate = "role.update";
    public const string RoleDelete = "role.delete";

    public const string AnalyticsView = "analytics.view";
    public const string AnalyticsExport = "analytics.export";
    public const string AnalyticsViewOwn = "analytics.view_own";

    public const string AuditRead = "audit.read";
    public const string AuditManage = "audit.manage";

    public const string SystemConfig = "system.config";
    public const string SystemLogs = "system.logs";
    public const string SystemBackup = "system.backup";

    public const string ContentModerate = "content.moderate";
    public const string ContentPublish = "content.publish";

    public static readonly string[] All =
    [
        PlaceRead, PlaceCreate, PlaceUpdate, PlaceDelete, PlaceApprove, PlaceToggle,
        TourRead, TourCreate, TourUpdate, TourDelete,
        UserRead, UserCreate, UserUpdate, UserDelete,
        RoleRead, RoleCreate, RoleUpdate, RoleDelete,
        AnalyticsView, AnalyticsExport, AnalyticsViewOwn,
        AuditRead, AuditManage,
        SystemConfig, SystemLogs, SystemBackup,
        ContentModerate, ContentPublish
    ];

    public static IReadOnlyList<string> DefaultPermissionsForRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return [];
        }

        var normalized = roleName.Trim().ToLowerInvariant();

        return normalized switch
        {
            AppRoles.SuperAdmin => All,
            AppRoles.Admin or AppRoles.EditorLegacyName =>
            [
                PlaceRead, PlaceCreate, PlaceUpdate, PlaceDelete, PlaceApprove, PlaceToggle,
                TourRead, TourCreate, TourUpdate, TourDelete,
                UserRead, UserCreate, UserUpdate, UserDelete,
                RoleRead, RoleCreate, RoleUpdate, RoleDelete,
                AnalyticsView, AnalyticsExport, AnalyticsViewOwn,
                AuditRead, AuditManage,
                SystemConfig, SystemLogs, SystemBackup,
                ContentModerate, ContentPublish
            ],
            AppRoles.User =>
            [
                PlaceRead,
                TourRead,
                AnalyticsViewOwn
            ],
            _ => []
        };
    }
}

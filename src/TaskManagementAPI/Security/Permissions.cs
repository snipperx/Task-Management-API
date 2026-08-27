using TaskManagementAPI.Domain;

namespace TaskManagementAPI.Security;

/// <summary>Canonical permission strings, emitted as "permissions" claims on the access token.</summary>
public static class Permissions
{
    public const string TasksView = "tasks:view";
    public const string TasksCreate = "tasks:create";
    public const string TasksEdit = "tasks:edit";
    public const string TasksDelete = "tasks:delete";
    public const string TasksAssign = "tasks:assign";
    public const string TasksStatusUpdate = "tasks:status-update";

    public const string ProjectsView = "projects:view";
    public const string ProjectsCreate = "projects:create";
    public const string ProjectsEdit = "projects:edit";
    public const string ProjectsDelete = "projects:delete";

    public const string UsersView = "users:view";
    public const string UsersManage = "users:manage";

    public const string CommentsCreate = "comments:create";
    public const string CommentsEdit = "comments:edit";
    public const string CommentsDelete = "comments:delete";

    public const string ReportsView = "reports:view";
    public const string ReportsGenerate = "reports:generate";

    public static readonly IReadOnlyList<string> All = new[]
    {
        TasksView, TasksCreate, TasksEdit, TasksDelete, TasksAssign, TasksStatusUpdate,
        ProjectsView, ProjectsCreate, ProjectsEdit, ProjectsDelete,
        UsersView, UsersManage,
        CommentsCreate, CommentsEdit, CommentsDelete,
        ReportsView, ReportsGenerate
    };
}

public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<UserRole, HashSet<string>> Map = new Dictionary<UserRole, HashSet<string>>
    {
        [UserRole.Viewer] = new()
        {
            Permissions.TasksView, Permissions.ProjectsView, Permissions.ReportsView
        },
        [UserRole.Developer] = new()
        {
            Permissions.TasksView, Permissions.TasksCreate, Permissions.TasksEdit, Permissions.TasksStatusUpdate,
            Permissions.ProjectsView,
            Permissions.CommentsCreate, Permissions.CommentsEdit, Permissions.CommentsDelete,
            Permissions.ReportsView
        },
        [UserRole.Manager] = new()
        {
            Permissions.TasksView, Permissions.TasksCreate, Permissions.TasksEdit, Permissions.TasksDelete,
            Permissions.TasksAssign, Permissions.TasksStatusUpdate,
            Permissions.ProjectsView, Permissions.ProjectsCreate, Permissions.ProjectsEdit, Permissions.ProjectsDelete,
            Permissions.UsersView,
            Permissions.CommentsCreate, Permissions.CommentsEdit, Permissions.CommentsDelete,
            Permissions.ReportsView, Permissions.ReportsGenerate
        },
        [UserRole.Admin] = new(Permissions.All)
    };

    public static IReadOnlyCollection<string> For(UserRole role)
        => Map.TryGetValue(role, out var perms) ? perms : Array.Empty<string>();

    public static bool Has(UserRole role, string permission)
        => role == UserRole.Admin || (Map.TryGetValue(role, out var perms) && perms.Contains(permission));
}

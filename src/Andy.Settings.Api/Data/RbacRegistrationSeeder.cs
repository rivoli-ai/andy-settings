namespace Andy.Settings.Api.Data;

/// <summary>
/// Documents and provides the RBAC permission and role configuration for Andy Settings.
/// This class captures the roles, permissions, and test user assignment that must exist
/// in Andy RBAC for the settings service to enforce access control.
/// </summary>
public class RbacRegistrationSeeder
{
    /// <summary>
    /// Returns the complete RBAC registration information required for Andy Settings.
    /// </summary>
    public static RbacRegistrationInfo GetRegistrationInfo()
    {
        var allPermissions = new[]
        {
            "definition:read", "definition:write", "definition:delete",
            "value:read", "value:write", "value:delete",
            "secret:read", "secret:write",
            "audit:read", "export:read", "import:write",
        };

        return new RbacRegistrationInfo(
            ApplicationCode: "settings",
            ApplicationName: "Andy Settings",
            Permissions: allPermissions,
            Roles: new[]
            {
                new RoleInfo(
                    "settings-admin",
                    "All permissions",
                    allPermissions),
                new RoleInfo(
                    "settings-editor",
                    "Read definitions, read/write values, read audit",
                    new[] { "definition:read", "value:read", "value:write", "audit:read" }),
                new RoleInfo(
                    "settings-viewer",
                    "Read-only access",
                    new[] { "definition:read", "value:read", "audit:read" }),
                new RoleInfo(
                    "settings-secret-admin",
                    "Editor + secret access",
                    new[] { "definition:read", "value:read", "value:write", "audit:read", "secret:read", "secret:write" }),
            },
            TestUserEmail: "test@andy.local",
            TestUserRole: "settings-admin"
        );
    }
}

/// <summary>
/// Describes the complete RBAC registration required for Andy Settings.
/// </summary>
/// <param name="ApplicationCode">The RBAC application identifier.</param>
/// <param name="ApplicationName">Human-readable application name.</param>
/// <param name="Permissions">All permission codes used by the application.</param>
/// <param name="Roles">Role definitions with their assigned permissions.</param>
/// <param name="TestUserEmail">Email of the seeded test user.</param>
/// <param name="TestUserRole">Role assigned to the test user.</param>
public record RbacRegistrationInfo(
    string ApplicationCode,
    string ApplicationName,
    string[] Permissions,
    RoleInfo[] Roles,
    string TestUserEmail,
    string TestUserRole
);

/// <summary>
/// Describes a single RBAC role and its assigned permissions.
/// </summary>
/// <param name="Name">The role identifier (e.g., "settings-admin").</param>
/// <param name="Description">Human-readable description of the role's purpose.</param>
/// <param name="Permissions">Permission codes assigned to this role.</param>
public record RoleInfo(
    string Name,
    string Description,
    string[] Permissions
);

namespace Andy.Settings.Api.Data;

/// <summary>
/// Documents and provides the required Andy Auth registration for the Andy Settings service.
/// This class captures the OAuth2/OIDC client registrations that must exist in Andy Auth
/// for the settings API, web UI, and CLI to function correctly.
/// </summary>
public class AuthRegistrationSeeder
{
    /// <summary>
    /// Returns the complete registration information required for Andy Settings in Andy Auth.
    /// </summary>
    public static AuthRegistrationInfo GetRegistrationInfo() => new(
        Audience: "urn:andy-settings-api",
        WebClientId: "andy-settings-web",
        CliClientId: "andy-settings-cli",
        RedirectUris: new[]
        {
            "https://localhost:5300/callback",
            "https://localhost:4200/callback",
        },
        TestUserEmail: "test@andy.local",
        WebClientDescription: "Andy Settings Web UI (Authorization Code + PKCE)",
        CliClientDescription: "Andy Settings CLI (Device Authorization Flow)",
        TestUserRole: "settings-admin"
    );
}

/// <summary>
/// Describes the Andy Auth registration required for the Andy Settings service.
/// </summary>
/// <param name="Audience">The API audience identifier (used as the JWT audience claim).</param>
/// <param name="WebClientId">OAuth2 client ID for the web UI (uses Authorization Code + PKCE).</param>
/// <param name="CliClientId">OAuth2 client ID for the CLI tool (uses Device Authorization Flow).</param>
/// <param name="RedirectUris">Allowed redirect URIs for the web client.</param>
/// <param name="TestUserEmail">Email address of the seeded test user.</param>
/// <param name="WebClientDescription">Human-readable description of the web client registration.</param>
/// <param name="CliClientDescription">Human-readable description of the CLI client registration.</param>
/// <param name="TestUserRole">RBAC role assigned to the test user.</param>
public record AuthRegistrationInfo(
    string Audience,
    string WebClientId,
    string CliClientId,
    string[] RedirectUris,
    string TestUserEmail,
    string WebClientDescription,
    string CliClientDescription,
    string TestUserRole
);

namespace Andy.Settings.Application.Interfaces;

/// <summary>
/// Extracts the current user's identity from the HTTP context (JWT claims).
/// </summary>
public interface ICurrentUserService
{
    string? GetUserId();
    string? GetUserName();
    string? GetEmail();
}

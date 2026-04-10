using System.Security.Claims;
using Andy.Settings.Application.Interfaces;

namespace Andy.Settings.Api.Services;

/// <summary>
/// Extracts current user identity from HTTP context JWT claims.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public string? GetUserId()
        => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

    public string? GetUserName()
        => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
           ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("name");

    public string? GetEmail()
        => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
           ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("email");
}

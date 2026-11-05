using System.Security.Claims;

namespace StockDesk.Web.ApiClient;

public static class UserClaimsExtensions
{
    public const string JwtClaimType = "stockdesk_jwt";

    public static string? GetJwt(this ClaimsPrincipal user) => user.FindFirstValue(JwtClaimType);

    public static string? GetUsername(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Name);

    public static string? GetRole(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role);

    public static bool IsAdmin(this ClaimsPrincipal user) => user.GetRole() == "Admin";
}

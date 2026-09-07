using System.Security.Claims;

namespace RTSC.Core.Security;

public static class SecurityPolicy
{
    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    public static bool RequiresApiAntiforgery(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api") || !UnsafeMethods.Contains(request.Method))
            return false;

        return !IsExternalWebhook(request.Path);
    }

    public static bool IsExternalWebhook(PathString path) =>
        path.Equals("/api/integrations/max/webhook", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/integrations/telegram/webhook", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/integrations/vk/callback", StringComparison.OrdinalIgnoreCase);

    public static string ClientKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
            return $"user:{userId}";

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    public static RateLimitProfile RateLimitFor(HttpContext context)
    {
        var path = context.Request.Path;

        if (path.StartsWithSegments("/health"))
            return new("health", 600, TimeSpan.FromMinutes(1));

        if (path.Equals("/Login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/Register", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/auth"))
            return new("auth", 12, TimeSpan.FromMinutes(1));

        if (IsExternalWebhook(path))
            return new("webhook", 300, TimeSpan.FromMinutes(1));

        if (path.StartsWithSegments("/api"))
            return new("api", 180, TimeSpan.FromMinutes(1));

        return new("web", 360, TimeSpan.FromMinutes(1));
    }

    public sealed record RateLimitProfile(string Name, int PermitLimit, TimeSpan Window);
}

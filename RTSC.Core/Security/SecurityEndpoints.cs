using Microsoft.AspNetCore.Antiforgery;

namespace RTSC.Core.Security;

public static class SecurityEndpoints
{
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/security/csrf", (HttpContext http, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Ok(new
            {
                requestToken = tokens.RequestToken,
                headerName = "X-CSRF-TOKEN"
            });
        }).AllowAnonymous();

        return app;
    }
}

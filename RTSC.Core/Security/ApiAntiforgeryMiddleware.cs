using Microsoft.AspNetCore.Antiforgery;

namespace RTSC.Core.Security;

public sealed class ApiAntiforgeryMiddleware(RequestDelegate next, ILogger<ApiAntiforgeryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (SecurityPolicy.RequiresApiAntiforgery(context.Request))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException exception)
            {
                logger.LogWarning(exception, "Rejected API request with invalid antiforgery token: {Method} {Path}", context.Request.Method, context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "antiforgery_validation_failed",
                    message = "Получите CSRF-токен через GET /api/security/csrf и передайте его в заголовке X-CSRF-TOKEN."
                });
                return;
            }
        }

        await next(context);
    }
}

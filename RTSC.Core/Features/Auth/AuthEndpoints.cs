using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, AppDbContext db, IPasswordHasher<User> hasher) =>
        {
            var displayName = request.DisplayName?.Trim() ?? string.Empty;
            var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var password = request.Password ?? string.Empty;

            if (displayName.Length is < 2 or > 200)
                return Results.BadRequest(new { error = "displayName must contain 2-200 characters" });
            if (email.Length is < 3 or > 320 || !email.Contains('@'))
                return Results.BadRequest(new { error = "valid email is required" });
            if (password.Length is < 8 or > 256)
                return Results.BadRequest(new { error = "password must contain 8-256 characters" });

            if (await db.Users.AnyAsync(x => x.Email == email))
                return Results.Conflict(new { error = "email already registered" });

            var user = new User
            {
                DisplayName = displayName,
                Email = email
            };
            user.PasswordHash = hasher.HashPassword(user, password);

            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/api/users/{user.Id}", new { user.Id, user.DisplayName, user.Email });
        });

        group.MapPost("/login", async (LoginRequest request, AppDbContext db, IPasswordHasher<User> hasher, HttpContext http) =>
        {
            var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var password = request.Password ?? string.Empty;
            if (email.Length > 320 || password.Length > 256)
                return Results.Unauthorized();

            var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
            if (user is null || user.Status != UserStatus.Active)
                return Results.Unauthorized();

            var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = hasher.HashPassword(user, password);
                await db.SaveChangesAsync();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.DisplayName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.ToString())
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = true });
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { user.Id, user.DisplayName, role = user.Role.ToString() });
        });

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            http.Response.Headers.CacheControl = "no-store";
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", (ClaimsPrincipal user, HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (user.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            return Results.Ok(new
            {
                id = user.FindFirstValue(ClaimTypes.NameIdentifier),
                name = user.Identity.Name,
                email = user.FindFirstValue(ClaimTypes.Email),
                role = user.FindFirstValue(ClaimTypes.Role)
            });
        });

        return app;
    }

    public sealed record RegisterRequest(string DisplayName, string Email, string Password);
    public sealed record LoginRequest(string Email, string Password);
}

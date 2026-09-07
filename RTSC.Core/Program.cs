using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.Auth;
using RTSC.Core.Features.CheckIn;
using RTSC.Core.Features.Communities;
using RTSC.Core.Features.Events;
using RTSC.Core.Features.ExternalAccounts;
using RTSC.Core.Features.Moderation;
using RTSC.Core.Features.Notifications;
using RTSC.Core.Features.Personalization;
using RTSC.Core.Features.Ratings;
using RTSC.Core.Features.Reminders;
using RTSC.Core.Features.Reports;
using RTSC.Core.Integrations.Max;
using RTSC.Core.Integrations.Telegram;
using RTSC.Core.Integrations.Vk;
using RTSC.Core.Security;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 2 * 1024 * 1024;
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "Admin");
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, ".data", "keys");

Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("RTSC.Core");

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = isDevelopment ? "rtsc.csrf" : "__Host-rtsc.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var profile = SecurityPolicy.RateLimitFor(context);
        var key = $"{profile.Name}:{SecurityPolicy.ClientKey(context)}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = profile.PermitLimit,
            Window = profile.Window,
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "rate_limit_exceeded",
            message = "Слишком много запросов. Повторите попытку позже."
        }, cancellationToken);
    };
});

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AccessControlService>();
builder.Services.AddScoped<ExternalLinkService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddSingleton<CommentModerationService>();
builder.Services.AddSingleton<CheckInTokenService>();
builder.Services.AddHostedService<BootstrapAdminService>();
builder.Services.AddHostedService<NotificationDispatcherWorker>();
builder.Services.AddHostedService<EventReminderWorker>();

builder.Services.Configure<MaxOptions>(builder.Configuration.GetSection(MaxOptions.SectionName));
builder.Services.AddHttpClient<MaxApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<MaxOptions>>().Value;
    if (Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var baseUri))
        client.BaseAddress = baseUri;

    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<INotificationChannelSender, MaxNotificationSender>();

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient<TelegramApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    if (Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var baseUri))
        client.BaseAddress = baseUri;
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<INotificationChannelSender, TelegramNotificationSender>();

builder.Services.Configure<VkOptions>(builder.Configuration.GetSection(VkOptions.SectionName));
builder.Services.AddHttpClient<VkApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<VkOptions>>().Value;
    if (Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var baseUri))
        client.BaseAddress = baseUri;
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<INotificationChannelSender, VkNotificationSender>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Forbidden";
        options.Cookie.Name = isDevelopment ? "rtsc.session" : "__Host-rtsc.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin", "SuperAdmin"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseAntiforgery();
app.UseMiddleware<ApiAntiforgeryMiddleware>();

app.MapRazorPages();
app.MapAuthEndpoints();
app.MapCommunityEndpoints();
app.MapEventEndpoints();
app.MapRatingEndpoints();
app.MapCheckInEndpoints();
app.MapReportEndpoints();
app.MapMaxIntegrationEndpoints();
app.MapTelegramIntegrationEndpoints();
app.MapVkIntegrationEndpoints();
app.MapSecurityEndpoints();

app.MapGet("/health/live", (HttpContext http) =>
{
    http.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { status = "ok", service = "RTSC.Core" });
}).AllowAnonymous();

app.MapGet("/health/ready", ReadinessAsync).AllowAnonymous();
app.MapGet("/health", ReadinessAsync).AllowAnonymous();

app.MapGet("/api/system/info", () => Results.Ok(new
{
    service = "RTSC.Core",
    version = "1.0.0",
    architecture = "modular-monolith",
    ui = "razor-pages",
    database = "postgresql",
    messaging = "outbox-style delivery queue",
    personalization = "explicit interests with transparent scoring",
    security = "antiforgery + rate-limiting + security-headers"
}));

app.Run();

static async Task<IResult> ReadinessAsync(HttpContext http, AppDbContext db, CancellationToken cancellationToken)
{
    http.Response.Headers.CacheControl = "no-store";
    try
    {
        var database = await db.Database.CanConnectAsync(cancellationToken);
        return database
            ? Results.Ok(new { status = "ready", service = "RTSC.Core", database = "ok" })
            : Results.Json(new { status = "not-ready", service = "RTSC.Core", database = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception)
    {
        return Results.Json(new { status = "not-ready", service = "RTSC.Core", database = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

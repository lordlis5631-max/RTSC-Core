using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
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
using RTSC.Core.Features.Ratings;
using RTSC.Core.Features.Reminders;
using RTSC.Core.Features.Reports;
using RTSC.Core.Integrations.Max;
using RTSC.Core.Integrations.Telegram;
using RTSC.Core.Integrations.Vk;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AccessControlService>();
builder.Services.AddScoped<ExternalLinkService>();
builder.Services.AddScoped<NotificationService>();
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
        options.Cookie.Name = "rtsc.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
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

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

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

app.MapGet("/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        var database = await db.Database.CanConnectAsync(cancellationToken);
        return database
            ? Results.Ok(new { status = "ok", service = "RTSC.Core", database = "ok" })
            : Results.Json(new { status = "degraded", service = "RTSC.Core", database = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception)
    {
        return Results.Json(new { status = "degraded", service = "RTSC.Core", database = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/system/info", () => Results.Ok(new
{
    service = "RTSC.Core",
    version = "0.5.0",
    architecture = "modular-monolith",
    ui = "razor-pages",
    database = "postgresql",
    messaging = "outbox-style delivery queue"
}));

app.Run();

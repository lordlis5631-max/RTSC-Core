using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.ExternalAccounts;
using RTSC.Core.Features.Notifications;
using RTSC.Core.Integrations.Max;
using RTSC.Core.Integrations.Telegram;
using RTSC.Core.Integrations.Vk;

namespace RTSC.Core.Pages.Profile;

[Authorize]
public sealed class IndexModel(
    AppDbContext db,
    ExternalLinkService linkService,
    NotificationService notificationService,
    IOptions<MaxOptions> maxOptions,
    IOptions<TelegramOptions> telegramOptions,
    IOptions<VkOptions> vkOptions) : PageModel
{
    public UserVm? CurrentUser { get; private set; }
    public IReadOnlyList<AccountVm> Accounts { get; private set; } = [];
    public IReadOnlyList<NotificationVm> Notifications { get; private set; } = [];
    public string? MaxBotUrl => maxOptions.Value.BotUrl;
    public string? TelegramBotUrl => telegramOptions.Value.BotUrl;
    public string? VkBotUrl => vkOptions.Value.BotUrl;

    [TempData] public string? Message { get; set; }
    [TempData] public string? LinkCode { get; set; }
    [TempData] public string? LinkExpiresAt { get; set; }
    [TempData] public string? LinkProvider { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AccessControlService.GetUserId(User);
        if (userId is null) return Challenge();

        await LoadAsync(userId.Value);
        return Page();
    }

    public Task<IActionResult> OnPostCreateMaxLinkAsync() => CreateLinkAsync(ExternalProvider.Max);

    public Task<IActionResult> OnPostCreateTelegramLinkAsync() => CreateLinkAsync(ExternalProvider.Telegram);

    public Task<IActionResult> OnPostCreateVkLinkAsync() => CreateLinkAsync(ExternalProvider.Vk);

    private async Task<IActionResult> CreateLinkAsync(ExternalProvider provider)
    {
        var userId = AccessControlService.GetUserId(User);
        if (userId is null) return Challenge();

        var link = await linkService.CreateAsync(userId.Value, provider);
        LinkCode = link.Code;
        LinkExpiresAt = link.ExpiresAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        LinkProvider = provider.ToString();
        Message = $"Код {provider} создан. Он действует 15 минут.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleNotificationsAsync(Guid accountId, bool enabled)
    {
        var userId = AccessControlService.GetUserId(User);
        if (userId is null) return Challenge();

        var account = await db.ExternalAccounts
            .SingleOrDefaultAsync(x => x.Id == accountId && x.UserId == userId.Value);

        if (account is null) return NotFound();

        account.NotificationsEnabled = enabled;
        await db.SaveChangesAsync();

        Message = enabled ? "Уведомления включены." : "Уведомления отключены.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUnlinkAsync(Guid accountId)
    {
        var userId = AccessControlService.GetUserId(User);
        if (userId is null) return Challenge();

        var account = await db.ExternalAccounts
            .SingleOrDefaultAsync(x => x.Id == accountId && x.UserId == userId.Value);

        if (account is null) return NotFound();

        db.ExternalAccounts.Remove(account);
        await db.SaveChangesAsync();

        Message = $"{account.Provider} отвязан.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkAllReadAsync()
    {
        var userId = AccessControlService.GetUserId(User);
        if (userId is null) return Challenge();

        await notificationService.MarkAllReadAsync(userId.Value);
        return RedirectToPage();
    }

    private async Task LoadAsync(Guid userId)
    {
        CurrentUser = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new UserVm(x.DisplayName, x.Email, x.Phone, x.Role.ToString(), x.CreatedAt))
            .SingleOrDefaultAsync();

        Accounts = await db.ExternalAccounts.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Provider)
            .Select(x => new AccountVm(
                x.Id,
                x.Provider.ToString(),
                x.ExternalUserId,
                x.Username,
                x.NotificationsEnabled,
                x.LinkedAt,
                x.LastSeenAt))
            .ToListAsync();

        Notifications = await db.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(30)
            .Select(x => new NotificationVm(
                x.Id,
                x.Type,
                x.Title,
                x.Body,
                x.TargetUrl,
                x.CreatedAt,
                x.ReadAt))
            .ToListAsync();
    }

    public sealed record UserVm(string DisplayName, string Email, string? Phone, string Role, DateTimeOffset CreatedAt);
    public sealed record AccountVm(Guid Id, string Provider, string ExternalUserId, string? Username, bool NotificationsEnabled, DateTimeOffset LinkedAt, DateTimeOffset? LastSeenAt);
    public sealed record NotificationVm(Guid Id, string Type, string Title, string Body, string? TargetUrl, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
}

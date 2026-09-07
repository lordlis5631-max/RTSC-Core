using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using RTSC.Core.Integrations.Max;
using RTSC.Core.Integrations.Telegram;
using RTSC.Core.Integrations.Vk;

namespace RTSC.Core.Pages.Admin;

public sealed class IntegrationsModel(
    IOptions<MaxOptions> maxOptions,
    MaxApiClient maxClient,
    IOptions<TelegramOptions> telegramOptions,
    TelegramApiClient telegramClient,
    IOptions<VkOptions> vkOptions,
    VkApiClient vkClient) : PageModel
{
    public IntegrationVm Max { get; private set; } = null!;
    public IntegrationVm Telegram { get; private set; } = null!;
    public VkVm Vk { get; private set; } = null!;

    [TempData] public string? Message { get; set; }

    public void OnGet() => Load();

    public async Task<IActionResult> OnPostConfigureMaxWebhookAsync()
    {
        var result = await maxClient.ConfigureWebhookAsync();
        Message = result.IsSuccess
            ? "Webhook MAX настроен."
            : $"MAX не принял настройку webhook: {result.Error}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConfigureTelegramWebhookAsync()
    {
        var result = await telegramClient.ConfigureWebhookAsync();
        Message = result.IsSuccess
            ? "Webhook Telegram настроен."
            : $"Telegram не принял настройку webhook: {result.Error}";
        return RedirectToPage();
    }

    private void Load()
    {
        var max = maxOptions.Value;
        Max = new IntegrationVm(
            maxClient.IsConfigured,
            max.HasValidWebhookUrl() && max.HasValidWebhookSecret(),
            max.WebhookUrl,
            max.BotUrl,
            max.PublicBaseUrl);

        var telegram = telegramOptions.Value;
        Telegram = new IntegrationVm(
            telegramClient.IsConfigured,
            telegram.HasValidWebhookUrl() && telegram.HasValidWebhookSecret(),
            telegram.WebhookUrl,
            telegram.BotUrl,
            telegram.PublicBaseUrl);

        var vk = vkOptions.Value;
        Vk = new VkVm(
            vkClient.IsConfigured,
            vk.HasValidCallbackUrl() && vk.HasValidCallbackSecret() && vk.HasValidConfirmationCode(),
            vk.CallbackUrl,
            vk.BotUrl,
            vk.GroupId,
            vk.ApiVersion,
            vk.HasValidCallbackSecret(),
            vk.HasValidConfirmationCode());
    }

    public sealed record IntegrationVm(bool Configured, bool WebhookConfigured, string? WebhookUrl, string? BotUrl, string? PublicBaseUrl);
    public sealed record VkVm(bool Configured, bool CallbackConfigured, string? CallbackUrl, string? BotUrl, string? GroupId, string? ApiVersion, bool SecretConfigured, bool ConfirmationConfigured);
}

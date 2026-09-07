using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RTSC.Core.Integrations.Telegram;

public sealed class TelegramApiClient(HttpClient httpClient, IOptions<TelegramOptions> options)
{
    private readonly TelegramOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Token);

    public async Task<TelegramApiResult> SendTextAsync(
        string externalUserId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return TelegramApiResult.Failure("telegram_token_not_configured");

        if (!long.TryParse(externalUserId, out var chatId))
            return TelegramApiResult.Failure("invalid_telegram_user_id");

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildMethodUrl("sendMessage"));
        request.Content = JsonContent.Create(new
        {
            chat_id = chatId,
            text = Limit(text, 4096)
        });

        return await SendAsync(request, cancellationToken, extractMessageId: true);
    }

    public async Task<TelegramApiResult> ConfigureWebhookAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return TelegramApiResult.Failure("telegram_token_not_configured");

        if (!_options.HasValidWebhookUrl())
            return TelegramApiResult.Failure("valid_https_webhook_url_required");

        if (!_options.HasValidWebhookSecret())
            return TelegramApiResult.Failure("webhook_secret_must_match_[A-Za-z0-9_-]{16,256}");

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildMethodUrl("setWebhook"));
        request.Content = JsonContent.Create(new
        {
            url = _options.WebhookUrl,
            secret_token = _options.WebhookSecret,
            allowed_updates = new[] { "message" },
            drop_pending_updates = false
        });

        return await SendAsync(request, cancellationToken, extractMessageId: false);
    }

    public string? BuildAbsoluteUrl(string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl)) return null;
        if (Uri.TryCreate(targetUrl, UriKind.Absolute, out var absolute)) return absolute.ToString();

        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl) ||
            !Uri.TryCreate(_options.PublicBaseUrl, UriKind.Absolute, out var baseUri))
            return null;

        return new Uri(baseUri, targetUrl).ToString();
    }

    public string? BuildStartUrl(string code)
    {
        if (string.IsNullOrWhiteSpace(_options.BotUrl) ||
            !Uri.TryCreate(_options.BotUrl, UriKind.Absolute, out var botUri))
            return null;

        var separator = string.IsNullOrEmpty(botUri.Query) ? "?" : "&";
        return $"{botUri}{separator}start={Uri.EscapeDataString(code)}";
    }

    private string BuildMethodUrl(string method) =>
        $"/bot{_options.Token}/{method}";

    private async Task<TelegramApiResult> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool extractMessageId)
    {
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return TelegramApiResult.Failure($"telegram_http_{(int)response.StatusCode}: {Limit(body, 1000)}");

            if (string.IsNullOrWhiteSpace(body))
                return TelegramApiResult.Failure("telegram_empty_response");

            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
            {
                var description = json.RootElement.TryGetProperty("description", out var error)
                    ? error.ToString()
                    : body;
                return TelegramApiResult.Failure($"telegram_api_rejected: {Limit(description, 1000)}");
            }

            string? messageId = null;
            if (extractMessageId &&
                json.RootElement.TryGetProperty("result", out var result) &&
                result.ValueKind == JsonValueKind.Object &&
                result.TryGetProperty("message_id", out var id))
                messageId = id.ToString();

            return TelegramApiResult.Success(messageId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TelegramApiResult.Failure($"telegram_request_failed: {ex.Message}");
        }
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record TelegramApiResult(bool IsSuccess, string? ExternalMessageId, string? Error)
{
    public static TelegramApiResult Success(string? externalMessageId = null) => new(true, externalMessageId, null);
    public static TelegramApiResult Failure(string error) => new(false, null, error);
}

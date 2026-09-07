using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RTSC.Core.Domain;
using RTSC.Core.Features.ExternalAccounts;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Integrations.Telegram;

public static class TelegramWebhookEndpoints
{
    public static IEndpointRouteBuilder MapTelegramIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/integrations/telegram/webhook", HandleWebhookAsync).AllowAnonymous();

        var admin = app.MapGroup("/api/integrations/telegram")
            .WithTags("Telegram")
            .RequireAuthorization("Admin");

        admin.MapGet("/status", (IOptions<TelegramOptions> options, TelegramApiClient client) =>
        {
            var value = options.Value;
            return Results.Ok(new
            {
                configured = client.IsConfigured,
                webhookConfigured = value.HasValidWebhookUrl() && value.HasValidWebhookSecret(),
                webhookUrl = value.WebhookUrl,
                botUrl = value.BotUrl,
                publicBaseUrl = value.PublicBaseUrl
            });
        });

        admin.MapPost("/webhook", async (TelegramApiClient client, CancellationToken cancellationToken) =>
        {
            var result = await client.ConfigureWebhookAsync(cancellationToken);
            return result.IsSuccess
                ? Results.Ok(new { success = true })
                : Results.BadRequest(new { success = false, error = result.Error });
        });

        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(
        HttpContext http,
        IOptions<TelegramOptions> options,
        ExternalLinkService linkService,
        NotificationService notifications,
        TelegramApiClient client,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("TelegramWebhook");
        var configuredSecret = options.Value.WebhookSecret;

        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            logger.LogError("Telegram webhook is disabled because WebhookSecret is not configured");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var actualSecret = http.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        if (!FixedTimeEquals(configuredSecret, actualSecret))
        {
            logger.LogWarning("Rejected Telegram webhook with invalid secret");
            return Results.Unauthorized();
        }

        if (http.Request.ContentLength is > 262144)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        using (document)
        {
            var root = document.RootElement;
            if (!TryGetObject(root, "message", out var message))
                return Results.Ok(new { ok = true });

            if (!TryGetObject(message, "from", out var from))
                return Results.Ok(new { ok = true });

            var userId = GetString(from, "id");
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Ok(new { ok = true });

            var username = GetString(from, "username");
            await linkService.TouchAsync(ExternalProvider.Telegram, userId, username, cancellationToken);

            var text = GetString(message, "text");
            if (!TryParseLinkCommand(text, out var code))
                return Results.Ok(new { ok = true });

            if (string.IsNullOrWhiteSpace(code))
            {
                await client.SendTextAsync(
                    userId,
                    "Код привязки не указан. Получите его в RTSC: Профиль → «Подключить Telegram».",
                    cancellationToken);
                return Results.Ok(new { ok = true });
            }

            var redeem = await linkService.RedeemAsync(
                ExternalProvider.Telegram,
                code,
                userId,
                username,
                cancellationToken);

            if (!redeem.IsSuccess)
            {
                var messageText = redeem.Error == "external_account_already_linked"
                    ? "Этот Telegram уже привязан к другому аккаунту RTSC."
                    : "Код не найден или истёк. Создайте новый код в профиле RTSC.";

                await client.SendTextAsync(userId, messageText, cancellationToken);
                return Results.Ok(new { ok = true });
            }

            await notifications.CreateAsync(
                redeem.UserId.GetValueOrDefault(),
                "account.telegram-linked",
                "Telegram подключён",
                "Аккаунт Telegram успешно привязан. Теперь сюда могут приходить уведомления RTSC.",
                "/Profile",
                cancellationToken);

            return Results.Ok(new { ok = true });
        }
    }

    private static bool TryParseLinkCommand(string? text, out string code)
    {
        code = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        var command = parts[0].Split('@', 2)[0].ToLowerInvariant();
        if (command is not ("/link" or "link" or "/start" or "start")) return false;

        code = parts.Length > 1 ? parts[1] : string.Empty;
        return true;
    }

    private static bool TryGetObject(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(name, out value) &&
               value.ValueKind == JsonValueKind.Object;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

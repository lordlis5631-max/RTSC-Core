using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.ExternalAccounts;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Integrations.Max;

public static class MaxWebhookEndpoints
{
    public static IEndpointRouteBuilder MapMaxIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/integrations/max/webhook", HandleWebhookAsync)
            .AllowAnonymous();

        var admin = app.MapGroup("/api/integrations/max")
            .WithTags("MAX")
            .RequireAuthorization("Admin");

        admin.MapGet("/status", (IOptions<MaxOptions> options, MaxApiClient client) =>
        {
            var value = options.Value;
            return Results.Ok(new
            {
                configured = client.IsConfigured,
                webhookConfigured = !string.IsNullOrWhiteSpace(value.WebhookUrl),
                webhookUrl = value.WebhookUrl,
                botUrl = value.BotUrl,
                publicBaseUrl = value.PublicBaseUrl
            });
        });

        admin.MapPost("/subscription", async (MaxApiClient client, CancellationToken cancellationToken) =>
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
        IOptions<MaxOptions> options,
        AppDbContext db,
        ExternalLinkService linkService,
        NotificationService notifications,
        MaxApiClient maxClient,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("MaxWebhook");
        var configuredSecret = options.Value.WebhookSecret;

        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            logger.LogError("MAX webhook is disabled because WebhookSecret is not configured");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var actualSecret = http.Request.Headers["X-Max-Bot-Api-Secret"].ToString();
        if (!FixedTimeEquals(configuredSecret, actualSecret))
        {
            logger.LogWarning("Rejected MAX webhook with invalid secret");
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
            var updateType = GetString(root, "update_type");

            if (string.Equals(updateType, "bot_started", StringComparison.OrdinalIgnoreCase))
            {
                var user = ExtractRootUser(root);
                if (user is not null)
                {
                    await linkService.TouchAsync(ExternalProvider.Max, user.Value.Id, user.Value.Username, cancellationToken);

                    var alreadyLinked = await db.ExternalAccounts.AsNoTracking()
                        .AnyAsync(x => x.Provider == ExternalProvider.Max && x.ExternalUserId == user.Value.Id, cancellationToken);

                    if (!alreadyLinked)
                    {
                        await maxClient.SendTextAsync(
                            user.Value.Id,
                            "RTSC: чтобы привязать MAX к профилю, откройте сайт → Профиль → «Подключить MAX», затем отправьте сюда команду /link КОД.",
                            cancellationToken);
                    }
                }

                return Results.Ok(new { ok = true });
            }

            if (!string.Equals(updateType, "message_created", StringComparison.OrdinalIgnoreCase))
                return Results.Ok(new { ok = true });

            var messageUser = ExtractMessageUser(root) ?? ExtractRootUser(root);
            if (messageUser is null)
                return Results.Ok(new { ok = true });

            await linkService.TouchAsync(ExternalProvider.Max, messageUser.Value.Id, messageUser.Value.Username, cancellationToken);

            var text = ExtractMessageText(root);
            if (!TryParseLinkCommand(text, out var code))
                return Results.Ok(new { ok = true });

            if (string.IsNullOrWhiteSpace(code))
            {
                await maxClient.SendTextAsync(
                    messageUser.Value.Id,
                    "Код привязки не указан. Получите его в RTSC: Профиль → «Подключить MAX».",
                    cancellationToken);
                return Results.Ok(new { ok = true });
            }

            var result = await linkService.RedeemAsync(
                ExternalProvider.Max,
                code,
                messageUser.Value.Id,
                messageUser.Value.Username,
                cancellationToken);

            if (!result.IsSuccess)
            {
                var errorText = result.Error == "external_account_already_linked"
                    ? "Этот MAX уже привязан к другому аккаунту RTSC."
                    : "Код не найден или истёк. Создайте новый код в профиле RTSC.";

                await maxClient.SendTextAsync(messageUser.Value.Id, errorText, cancellationToken);
                return Results.Ok(new { ok = true });
            }

            await notifications.CreateAsync(
                result.UserId.GetValueOrDefault(),
                "account.max-linked",
                "MAX подключён",
                "Аккаунт MAX успешно привязан. Теперь сюда могут приходить уведомления RTSC.",
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

        var command = parts[0].Trim().ToLowerInvariant();
        if (command is not ("/link" or "link" or "/start" or "start"))
            return false;

        code = parts.Length > 1 ? parts[1] : string.Empty;
        return true;
    }

    private static (string Id, string? Username)? ExtractRootUser(JsonElement root)
    {
        if (!TryGetObject(root, "user", out var user)) return null;
        return ExtractUser(user);
    }

    private static (string Id, string? Username)? ExtractMessageUser(JsonElement root)
    {
        if (!TryGetObject(root, "message", out var message)) return null;
        if (!TryGetObject(message, "sender", out var sender)) return null;
        return ExtractUser(sender);
    }

    private static (string Id, string? Username)? ExtractUser(JsonElement user)
    {
        var id = GetString(user, "user_id") ?? GetString(user, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;
        return (id, GetString(user, "username"));
    }

    private static string? ExtractMessageText(JsonElement root)
    {
        if (!TryGetObject(root, "message", out var message)) return null;
        if (!TryGetObject(message, "body", out var body)) return null;
        return GetString(body, "text");
    }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out value) &&
               value.ValueKind == JsonValueKind.Object;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
            return null;

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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RTSC.Core.Domain;
using RTSC.Core.Features.ExternalAccounts;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Integrations.Vk;

public static class VkCallbackEndpoints
{
    public static IEndpointRouteBuilder MapVkIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/integrations/vk/callback", HandleCallbackAsync).AllowAnonymous();

        var admin = app.MapGroup("/api/integrations/vk")
            .WithTags("VK")
            .RequireAuthorization("Admin");

        admin.MapGet("/status", (IOptions<VkOptions> options, VkApiClient client) =>
        {
            var value = options.Value;
            return Results.Ok(new
            {
                configured = client.IsConfigured,
                callbackConfigured = value.HasValidCallbackUrl() && value.HasValidCallbackSecret() && value.HasValidConfirmationCode(),
                callbackUrl = value.CallbackUrl,
                botUrl = value.BotUrl,
                groupId = value.GroupId,
                apiVersion = value.ApiVersion
            });
        });

        return app;
    }

    private static async Task<IResult> HandleCallbackAsync(
        HttpContext http,
        IOptions<VkOptions> options,
        ExternalLinkService linkService,
        NotificationService notifications,
        VkApiClient client,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("VkCallback");
        var value = options.Value;

        if (!value.HasValidGroupId() || !value.HasValidCallbackSecret() || !value.HasValidConfirmationCode())
        {
            logger.LogError("VK callback is disabled because group/secret/confirmation settings are incomplete");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
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
            return Results.BadRequest("invalid_json");
        }

        using (document)
        {
            var root = document.RootElement;
            var groupId = GetString(root, "group_id");
            var secret = GetString(root, "secret");

            if (!string.Equals(groupId, value.GroupId, StringComparison.Ordinal) ||
                !FixedTimeEquals(value.CallbackSecret!, secret ?? string.Empty))
            {
                logger.LogWarning("Rejected VK callback with invalid group or secret");
                return Results.Unauthorized();
            }

            var type = GetString(root, "type");
            if (string.Equals(type, "confirmation", StringComparison.OrdinalIgnoreCase))
                return Results.Text(value.ConfirmationCode!, "text/plain", Encoding.UTF8);

            if (!string.Equals(type, "message_new", StringComparison.OrdinalIgnoreCase))
                return Results.Text("ok", "text/plain", Encoding.UTF8);

            if (!TryGetObject(root, "object", out var objectNode) ||
                !TryGetObject(objectNode, "message", out var message))
                return Results.Text("ok", "text/plain", Encoding.UTF8);

            var fromId = GetString(message, "from_id");
            if (string.IsNullOrWhiteSpace(fromId) || !long.TryParse(fromId, out var numericId) || numericId <= 0)
                return Results.Text("ok", "text/plain", Encoding.UTF8);

            await linkService.TouchAsync(ExternalProvider.Vk, fromId, null, cancellationToken);

            var text = GetString(message, "text");
            if (!TryParseLinkCommand(text, out var code))
                return Results.Text("ok", "text/plain", Encoding.UTF8);

            if (string.IsNullOrWhiteSpace(code))
            {
                await client.SendTextAsync(
                    fromId,
                    "Код привязки не указан. Получите его в RTSC: Профиль → «Подключить VK».",
                    cancellationToken);
                return Results.Text("ok", "text/plain", Encoding.UTF8);
            }

            var redeem = await linkService.RedeemAsync(
                ExternalProvider.Vk,
                code,
                fromId,
                null,
                cancellationToken);

            if (!redeem.IsSuccess)
            {
                var errorText = redeem.Error == "external_account_already_linked"
                    ? "Этот VK уже привязан к другому аккаунту RTSC."
                    : "Код не найден или истёк. Создайте новый код в профиле RTSC.";

                await client.SendTextAsync(fromId, errorText, cancellationToken);
                return Results.Text("ok", "text/plain", Encoding.UTF8);
            }

            await notifications.CreateAsync(
                redeem.UserId.GetValueOrDefault(),
                "account.vk-linked",
                "VK подключён",
                "Аккаунт VK успешно привязан. Теперь сюда могут приходить уведомления RTSC.",
                "/Profile",
                cancellationToken);

            return Results.Text("ok", "text/plain", Encoding.UTF8);
        }
    }

    private static bool TryParseLinkCommand(string? text, out string code)
    {
        code = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        var command = parts[0].ToLowerInvariant();
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

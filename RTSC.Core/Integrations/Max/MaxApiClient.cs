using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RTSC.Core.Integrations.Max;

public sealed class MaxApiClient(HttpClient httpClient, IOptions<MaxOptions> options)
{
    private readonly MaxOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Token);

    public async Task<MaxApiResult> SendTextAsync(
        string externalUserId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return MaxApiResult.Failure("max_token_not_configured");

        if (!long.TryParse(externalUserId, out _))
            return MaxApiResult.Failure("invalid_max_user_id");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/messages?user_id={Uri.EscapeDataString(externalUserId)}");

        request.Headers.TryAddWithoutValidation("Authorization", _options.Token);
        request.Content = JsonContent.Create(new
        {
            text = Limit(text, 4000)
        });

        return await SendAsync(request, cancellationToken);
    }

    public async Task<MaxApiResult> ConfigureWebhookAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return MaxApiResult.Failure("max_token_not_configured");

        if (!_options.HasValidWebhookUrl())
            return MaxApiResult.Failure("valid_https_webhook_url_required");

        if (!_options.HasValidWebhookSecret())
            return MaxApiResult.Failure("webhook_secret_must_match_[A-Za-z0-9_-]{16,256}");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/subscriptions");
        request.Headers.TryAddWithoutValidation("Authorization", _options.Token);
        request.Content = JsonContent.Create(new
        {
            url = _options.WebhookUrl,
            update_types = new[] { "message_created", "bot_started" },
            secret = _options.WebhookSecret
        });

        return await SendAsync(request, cancellationToken, requireSuccessFlag: true);
    }

    public string? BuildAbsoluteUrl(string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl)) return null;

        if (Uri.TryCreate(targetUrl, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl) ||
            !Uri.TryCreate(_options.PublicBaseUrl, UriKind.Absolute, out var baseUri))
            return null;

        return new Uri(baseUri, targetUrl).ToString();
    }

    private async Task<MaxApiResult> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool requireSuccessFlag = false)
    {
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return MaxApiResult.Failure($"max_http_{(int)response.StatusCode}: {Limit(body, 1000)}");

            string? externalId = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var json = JsonDocument.Parse(body);

                    if (requireSuccessFlag)
                    {
                        if (!json.RootElement.TryGetProperty("success", out var success) ||
                            success.ValueKind != JsonValueKind.True)
                        {
                            var message = json.RootElement.TryGetProperty("message", out var errorMessage)
                                ? errorMessage.ToString()
                                : body;
                            return MaxApiResult.Failure($"max_api_rejected: {Limit(message, 1000)}");
                        }
                    }

                    externalId = FindMessageId(json.RootElement);
                }
                catch (JsonException)
                {
                    if (requireSuccessFlag)
                        return MaxApiResult.Failure("max_invalid_json_response");
                }
            }
            else if (requireSuccessFlag)
            {
                return MaxApiResult.Failure("max_empty_response");
            }

            return MaxApiResult.Success(externalId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MaxApiResult.Failure($"max_request_failed: {ex.Message}");
        }
    }

    private static string? FindMessageId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;

        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
        {
            if (message.TryGetProperty("body", out var body) &&
                body.ValueKind == JsonValueKind.Object &&
                body.TryGetProperty("mid", out var nestedMid))
                return nestedMid.ToString();

            if (message.TryGetProperty("mid", out var messageMid))
                return messageMid.ToString();
        }

        if (root.TryGetProperty("body", out var rootBody) &&
            rootBody.ValueKind == JsonValueKind.Object &&
            rootBody.TryGetProperty("mid", out var rootMid))
            return rootMid.ToString();

        return null;
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record MaxApiResult(bool IsSuccess, string? ExternalMessageId, string? Error)
{
    public static MaxApiResult Success(string? externalMessageId = null) => new(true, externalMessageId, null);
    public static MaxApiResult Failure(string error) => new(false, null, error);
}

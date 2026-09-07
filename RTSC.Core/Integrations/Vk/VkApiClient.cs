using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RTSC.Core.Integrations.Vk;

public sealed class VkApiClient(HttpClient httpClient, IOptions<VkOptions> options)
{
    private readonly VkOptions _options = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Token) &&
        _options.HasValidGroupId();

    public async Task<VkApiResult> SendTextAsync(
        string externalUserId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return VkApiResult.Failure("vk_token_or_group_not_configured");

        if (!long.TryParse(externalUserId, out var peerId) || peerId <= 0)
            return VkApiResult.Failure("invalid_vk_user_id");

        var values = new Dictionary<string, string>
        {
            ["access_token"] = _options.Token!,
            ["v"] = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "5.199" : _options.ApiVersion,
            ["peer_id"] = peerId.ToString(),
            ["random_id"] = Random.Shared.Next(1, int.MaxValue).ToString(),
            ["message"] = Limit(text, 4000)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/method/messages.send")
        {
            Content = new FormUrlEncodedContent(values)
        };

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return VkApiResult.Failure($"vk_http_{(int)response.StatusCode}: {Limit(body, 1000)}");

            if (string.IsNullOrWhiteSpace(body))
                return VkApiResult.Failure("vk_empty_response");

            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("error_msg", out var errorMessage)
                    ? errorMessage.ToString()
                    : error.ToString();
                return VkApiResult.Failure($"vk_api_rejected: {Limit(message, 1000)}");
            }

            if (!json.RootElement.TryGetProperty("response", out var result))
                return VkApiResult.Failure("vk_response_missing");

            var externalId = result.ValueKind switch
            {
                JsonValueKind.Number => result.GetRawText(),
                JsonValueKind.String => result.GetString(),
                _ => null
            };

            return VkApiResult.Success(externalId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return VkApiResult.Failure($"vk_request_failed: {ex.Message}");
        }
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

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record VkApiResult(bool IsSuccess, string? ExternalMessageId, string? Error)
{
    public static VkApiResult Success(string? externalMessageId = null) => new(true, externalMessageId, null);
    public static VkApiResult Failure(string error) => new(false, null, error);
}

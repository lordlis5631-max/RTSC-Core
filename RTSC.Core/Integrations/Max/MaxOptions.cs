using System.Text.RegularExpressions;

namespace RTSC.Core.Integrations.Max;

public sealed partial class MaxOptions
{
    public const string SectionName = "Max";

    public string ApiBaseUrl { get; set; } = "https://platform-api2.max.ru";
    public string? Token { get; set; }
    public string? WebhookSecret { get; set; }
    public string? WebhookUrl { get; set; }
    public string? BotUrl { get; set; }
    public string? PublicBaseUrl { get; set; }

    public bool HasValidWebhookSecret() =>
        !string.IsNullOrWhiteSpace(WebhookSecret) && WebhookSecretRegex().IsMatch(WebhookSecret);

    public bool HasValidWebhookUrl() =>
        !string.IsNullOrWhiteSpace(WebhookUrl) &&
        Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;

    [GeneratedRegex("^[A-Za-z0-9_-]{16,256}$", RegexOptions.CultureInvariant)]
    private static partial Regex WebhookSecretRegex();
}

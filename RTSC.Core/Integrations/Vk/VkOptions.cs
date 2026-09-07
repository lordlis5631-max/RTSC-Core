using System.Text.RegularExpressions;

namespace RTSC.Core.Integrations.Vk;

public sealed partial class VkOptions
{
    public const string SectionName = "Vk";

    public string ApiBaseUrl { get; set; } = "https://api.vk.com";
    public string ApiVersion { get; set; } = "5.199";
    public string? Token { get; set; }
    public string? GroupId { get; set; }
    public string? CallbackSecret { get; set; }
    public string? ConfirmationCode { get; set; }
    public string? CallbackUrl { get; set; }
    public string? BotUrl { get; set; }
    public string? PublicBaseUrl { get; set; }

    public bool HasValidGroupId() => long.TryParse(GroupId, out var value) && value > 0;

    public bool HasValidCallbackSecret() =>
        !string.IsNullOrWhiteSpace(CallbackSecret) && CallbackSecretRegex().IsMatch(CallbackSecret);

    public bool HasValidConfirmationCode() =>
        !string.IsNullOrWhiteSpace(ConfirmationCode) && ConfirmationCode.Length <= 256;

    public bool HasValidCallbackUrl() =>
        !string.IsNullOrWhiteSpace(CallbackUrl) &&
        Uri.TryCreate(CallbackUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;

    [GeneratedRegex("^[A-Za-z0-9_.-]{16,256}$", RegexOptions.CultureInvariant)]
    private static partial Regex CallbackSecretRegex();
}

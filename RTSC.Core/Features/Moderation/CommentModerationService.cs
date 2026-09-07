using RTSC.Core.Domain;

namespace RTSC.Core.Features.Moderation;

public sealed class CommentModerationService
{
    // Intentionally small starter list. It is centralized so it can later be moved to DB/admin settings.
    private static readonly string[] RiskTerms =
    [
        "идиот", "дебил", "тупой", "тупая", "урод", "мразь", "сука", "бляд", "пидор", "хуй", "нахуй"
    ];

    public CommentStatus GetInitialStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return CommentStatus.Moderation;
        return RiskTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))
            ? CommentStatus.Moderation
            : CommentStatus.Published;
    }
}

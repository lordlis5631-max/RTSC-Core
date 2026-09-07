using RTSC.Core.Domain;
using RTSC.Core.Features.Moderation;
using RTSC.Core.Features.Personalization;

namespace RTSC.Core.Tests;

public sealed class CoreBehaviorTests
{
    [Fact]
    public void SafeCommentIsPublishedImmediately()
    {
        var service = new CommentModerationService();
        Assert.Equal(CommentStatus.Published, service.GetInitialStatus("Спасибо организаторам за мероприятие"));
    }

    [Fact]
    public void RiskCommentGoesToModeration()
    {
        var service = new CommentModerationService();
        Assert.Equal(CommentStatus.Moderation, service.GetInitialStatus("Ты идиот"));
    }

    [Theory]
    [InlineData(EventCategory.Technology, "Технологии")]
    [InlineData(EventCategory.Games, "Игры")]
    [InlineData(EventCategory.Volunteering, "Волонтёрство")]
    public void CategoryLabelsAreStable(EventCategory category, string expected)
    {
        Assert.Equal(expected, EventCategoryCatalog.Label(category));
    }
}

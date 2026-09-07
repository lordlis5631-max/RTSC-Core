using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages.Admin;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public int Users { get; private set; }
    public int PendingCommunities { get; private set; }
    public int PendingEvents { get; private set; }
    public int PendingComments { get; private set; }
    public int DeliveryProblems { get; private set; }
    public int LinkedMaxAccounts { get; private set; }

    public async Task OnGetAsync()
    {
        Users = await db.Users.CountAsync();
        PendingCommunities = await db.Communities.CountAsync(x => x.Status == CommunityStatus.Moderation);
        PendingEvents = await db.Events.CountAsync(x => x.Status == EventStatus.Moderation);
        PendingComments = await db.Comments.CountAsync(x => x.Status == CommentStatus.Moderation);
        DeliveryProblems = await db.NotificationDeliveries.CountAsync(x =>
            x.Status == NotificationDeliveryStatus.Failed ||
            x.Status == NotificationDeliveryStatus.Dead);
        LinkedMaxAccounts = await db.ExternalAccounts.CountAsync(x => x.Provider == ExternalProvider.Max);
    }
}

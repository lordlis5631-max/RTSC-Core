using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages.Admin;

public sealed class NotificationsModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<ItemVm> Items { get; private set; } = [];

    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Items = await db.NotificationDeliveries
            .AsNoTracking()
            .OrderBy(x => x.Status == NotificationDeliveryStatus.Dead ? 0 : x.Status == NotificationDeliveryStatus.Failed ? 1 : 2)
            .ThenByDescending(x => x.Notification.CreatedAt)
            .Take(150)
            .Select(x => new ItemVm(
                x.Id,
                x.Notification.User.DisplayName,
                x.Notification.User.Email,
                x.Notification.Title,
                x.Channel.ToString(),
                x.Status.ToString(),
                x.Attempts,
                x.Notification.CreatedAt,
                x.LastAttemptAt,
                x.SentAt,
                x.Error))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostRetryAsync(Guid id)
    {
        var delivery = await db.NotificationDeliveries.SingleOrDefaultAsync(x => x.Id == id);
        if (delivery is null) return NotFound();

        delivery.Status = NotificationDeliveryStatus.Pending;
        delivery.Attempts = 0;
        delivery.NextAttemptAt = DateTimeOffset.UtcNow;
        delivery.LastAttemptAt = null;
        delivery.SentAt = null;
        delivery.Error = null;
        await db.SaveChangesAsync();

        Message = "Доставка возвращена в очередь.";
        return RedirectToPage();
    }

    public sealed record ItemVm(
        Guid Id,
        string UserName,
        string Email,
        string Title,
        string Channel,
        string Status,
        int Attempts,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastAttemptAt,
        DateTimeOffset? SentAt,
        string? Error);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Events;

[Authorize]
public sealed class TicketModel(AppDbContext db) : PageModel
{
    public Guid Id { get; private set; }
    public string EventTitle { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var uid = AccessControlService.GetUserId(User); if (uid is null) return Challenge();
        var registered = await db.EventParticipants.AnyAsync(x => x.EventId == id && x.UserId == uid.Value && x.Status != ParticipantStatus.Cancelled); if (!registered) return NotFound();
        EventTitle = await db.Events.Where(x => x.Id == id).Select(x => x.Title).SingleOrDefaultAsync() ?? string.Empty; if (EventTitle.Length == 0) return NotFound();
        Id = id; UserName = User.Identity?.Name ?? string.Empty; return Page();
    }
}

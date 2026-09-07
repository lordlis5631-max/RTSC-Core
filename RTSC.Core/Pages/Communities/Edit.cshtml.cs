using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Communities;

[Authorize]
public sealed class EditModel(AppDbContext db, AccessControlService access) : PageModel
{
    public Guid Id { get; private set; }
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? LogoUrl { get; set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await access.CanManageCommunityAsync(id, User)) return Forbid();
        var item = await db.Communities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        Id = id; Name = item.Name; Description = item.Description; LogoUrl = item.LogoUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        Id = id;
        if (!await access.CanManageCommunityAsync(id, User)) return Forbid();
        if (string.IsNullOrWhiteSpace(Name)) { Error = "Укажите название."; return Page(); }
        var item = await db.Communities.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        item.Name = Name.Trim(); item.Description = Description?.Trim() ?? string.Empty; item.LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim();
        if (item.Status == CommunityStatus.Rejected) item.Status = CommunityStatus.Draft;
        await db.SaveChangesAsync();
        return RedirectToPage("/Communities/Details", new { id });
    }
}

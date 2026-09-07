using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Admin;

public sealed class UsersModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Query { get; set; }
    public IReadOnlyList<ItemVm> Items { get; private set; } = [];
    public async Task OnGetAsync()
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Query)) { var q = $"%{Query.Trim()}%"; query = query.Where(x => EF.Functions.ILike(x.DisplayName, q) || EF.Functions.ILike(x.Email, q)); }
        Items = await query.OrderBy(x => x.DisplayName).Take(300).Select(x => new ItemVm(x.Id, x.DisplayName, x.Email, x.Role.ToString(), x.Status.ToString())).ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleBlockAsync(Guid id)
    {
        var currentId = AccessControlService.GetUserId(User); if (currentId == id) return BadRequest();
        var item = await db.Users.SingleOrDefaultAsync(x => x.Id == id); if (item is null) return NotFound(); item.Status = item.Status == UserStatus.Active ? UserStatus.Blocked : UserStatus.Active; await db.SaveChangesAsync(); return RedirectToPage(new { q = Query });
    }

    public async Task<IActionResult> OnPostRoleAsync(Guid id, GlobalRole role)
    {
        if (!User.IsInRole(GlobalRole.SuperAdmin.ToString())) return Forbid();
        var item = await db.Users.SingleOrDefaultAsync(x => x.Id == id); if (item is null) return NotFound(); item.Role = role; await db.SaveChangesAsync(); return RedirectToPage(new { q = Query });
    }

    public sealed record ItemVm(Guid Id, string Name, string Email, string Role, string Status);
}

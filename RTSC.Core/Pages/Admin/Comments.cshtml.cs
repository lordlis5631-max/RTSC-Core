using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages.Admin;

public sealed class CommentsModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<ItemVm> Items { get; private set; } = [];
    public async Task OnGetAsync()
    {
        Items = await (from c in db.Comments.AsNoTracking()
                       join u in db.Users.AsNoTracking() on c.AuthorUserId equals u.Id
                       orderby c.Status == CommentStatus.Moderation ? 0 : 1, c.CreatedAt descending
                       select new ItemVm(c.Id, u.DisplayName, c.Text, c.Status.ToString(), c.CreatedAt)).Take(300).ToListAsync();
    }
    public async Task<IActionResult> OnPostStatusAsync(Guid id, CommentStatus status)
    {
        if (status is not (CommentStatus.Published or CommentStatus.Hidden or CommentStatus.Deleted or CommentStatus.Moderation)) return BadRequest();
        var item = await db.Comments.SingleOrDefaultAsync(x => x.Id == id); if (item is null) return NotFound(); item.Status = status; await db.SaveChangesAsync(); return RedirectToPage();
    }
    public sealed record ItemVm(Guid Id, string AuthorName, string Text, string Status, DateTimeOffset CreatedAt);
}

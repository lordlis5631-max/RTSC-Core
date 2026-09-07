using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;

namespace RTSC.Core.Pages;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public int UsersCount { get; private set; }
    public int CommunitiesCount { get; private set; }
    public int EventsCount { get; private set; }

    public async Task OnGetAsync()
    {
        UsersCount = await db.Users.CountAsync();
        CommunitiesCount = await db.Communities.CountAsync();
        EventsCount = await db.Events.CountAsync();
    }
}

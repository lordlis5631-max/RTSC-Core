using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Communities;

[Authorize]
public sealed class CreateModel(AppDbContext db) : PageModel
{
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? LogoUrl { get; set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = AccessControlService.GetUserId(User);
        if (userId is null) return Challenge();
        if (string.IsNullOrWhiteSpace(Name)) { Error = "Укажите название сообщества."; return Page(); }

        var community = new Community
        {
            Name = Name.Trim(),
            Description = Description?.Trim() ?? string.Empty,
            LogoUrl = string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim(),
            Status = CommunityStatus.Draft
        };
        db.Communities.Add(community);
        db.CommunityMembers.Add(new CommunityMember { CommunityId = community.Id, UserId = userId.Value, Role = CommunityMemberRole.Owner });
        await db.SaveChangesAsync();
        return RedirectToPage("/Communities/Details", new { id = community.Id });
    }
}

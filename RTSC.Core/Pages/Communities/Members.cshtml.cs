using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Pages.Communities;

[Authorize]
public sealed class MembersModel(AppDbContext db, AccessControlService access, NotificationService notifications) : PageModel
{
    public Guid CommunityId { get; private set; }
    public string CommunityName { get; private set; } = string.Empty;
    public IReadOnlyList<MemberVm> Members { get; private set; } = [];

    [BindProperty]
    public string AdminEmail { get; set; } = string.Empty;

    [TempData]
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await access.CanManageCommunityMembersAsync(id, User))
            return Forbid();

        if (!await LoadAsync(id))
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAddAdminAsync(Guid id)
    {
        if (!await access.CanManageCommunityMembersAsync(id, User))
            return Forbid();

        var email = AdminEmail.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.Status == UserStatus.Active);
        if (user is null)
        {
            Message = "Активный пользователь с таким email не найден.";
            return RedirectToPage(new { id });
        }

        var member = await db.CommunityMembers.SingleOrDefaultAsync(x => x.CommunityId == id && x.UserId == user.Id);
        if (member?.Role == CommunityMemberRole.Owner)
        {
            Message = "Владелец сообщества уже имеет максимальные права.";
            return RedirectToPage(new { id });
        }

        if (member is null)
        {
            member = new CommunityMember
            {
                CommunityId = id,
                UserId = user.Id,
                Role = CommunityMemberRole.Admin
            };
            db.CommunityMembers.Add(member);
        }
        else
        {
            member.Role = CommunityMemberRole.Admin;
        }

        var communityName = await db.Communities.Where(x => x.Id == id).Select(x => x.Name).SingleOrDefaultAsync();
        if (communityName is null)
            return NotFound();

        await db.SaveChangesAsync();
        await notifications.CreateAsync(
            user.Id,
            "community.admin-assigned",
            "Вы назначены администратором",
            $"Вам выданы права администратора сообщества «{communityName}».",
            $"/Communities/{id}");

        Message = "Администратор назначен.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoveAdminAsync(Guid id, Guid userId)
    {
        if (!await access.CanManageCommunityMembersAsync(id, User))
            return Forbid();

        var member = await db.CommunityMembers.SingleOrDefaultAsync(x =>
            x.CommunityId == id && x.UserId == userId);

        if (member is null)
            return RedirectToPage(new { id });

        if (member.Role == CommunityMemberRole.Owner)
        {
            Message = "Владельца нельзя удалить через управление администраторами.";
            return RedirectToPage(new { id });
        }

        if (member.Role == CommunityMemberRole.Admin)
        {
            db.CommunityMembers.Remove(member);
            var communityName = await db.Communities.Where(x => x.Id == id).Select(x => x.Name).SingleAsync();
            await db.SaveChangesAsync();
            await notifications.CreateAsync(
                userId,
                "community.admin-removed",
                "Права администратора сняты",
                $"Ваши права администратора сообщества «{communityName}» были сняты.",
                $"/Communities/{id}");
            Message = "Администратор удалён.";
        }

        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(Guid id)
    {
        var community = await db.Communities.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Name })
            .SingleOrDefaultAsync();

        if (community is null)
            return false;

        CommunityId = community.Id;
        CommunityName = community.Name;
        Members = await db.CommunityMembers.AsNoTracking()
            .Where(x => x.CommunityId == id)
            .OrderByDescending(x => x.Role)
            .ThenBy(x => x.User.DisplayName)
            .Select(x => new MemberVm(x.UserId, x.User.DisplayName, x.User.Email, x.Role.ToString(), x.JoinedAt))
            .ToListAsync();

        return true;
    }

    public sealed record MemberVm(Guid UserId, string Name, string Email, string Role, DateTimeOffset JoinedAt);
}

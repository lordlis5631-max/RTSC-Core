using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Events;

[Authorize]
public sealed class CreateModel(AppDbContext db, AccessControlService access) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? CommunityId { get; set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? Place { get; set; }
    [BindProperty] public double? Latitude { get; set; }
    [BindProperty] public double? Longitude { get; set; }
    [BindProperty] public int? Capacity { get; set; }
    [BindProperty] public string StartAtLocal { get; set; } = DateTime.Now.AddDays(1).ToString("yyyy-MM-ddTHH:mm");
    [BindProperty] public string? EndAtLocal { get; set; }
    [BindProperty] public string? RegistrationStartLocal { get; set; }
    [BindProperty] public string? RegistrationEndLocal { get; set; }
    [BindProperty] public string? ImageUrl { get; set; }
    public string? Error { get; private set; }
    public IReadOnlyList<CommunityVm> Communities { get; private set; } = [];

    public async Task OnGetAsync() => await LoadCommunitiesAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCommunitiesAsync();
        if (CommunityId is null || !await access.CanManageCommunityAsync(CommunityId.Value, User)) return Forbid();
        if (string.IsNullOrWhiteSpace(Title) || !TryLocal(StartAtLocal, out var start)) { Error = "Заполните название и дату начала."; return Page(); }
        DateTimeOffset? end = TryLocal(EndAtLocal, out var parsedEnd) ? parsedEnd : null;
        DateTimeOffset? regStart = TryLocal(RegistrationStartLocal, out var parsedRegStart) ? parsedRegStart : null;
        DateTimeOffset? regEnd = TryLocal(RegistrationEndLocal, out var parsedRegEnd) ? parsedRegEnd : null;
        if (end is not null && end <= start) { Error = "Окончание должно быть позже начала."; return Page(); }
        if (Capacity is <= 0) { Error = "Лимит участников должен быть больше нуля."; return Page(); }
        if (regStart is not null && regEnd is not null && regEnd <= regStart) { Error = "Дата окончания регистрации должна быть позже даты начала."; return Page(); }
        if (!ValidateCoordinates()) return Page();

        var evt = new Event
        {
            CommunityId = CommunityId.Value, Title = Title.Trim(), Description = Description?.Trim() ?? string.Empty,
            StartAt = start, EndAt = end, Place = Place?.Trim() ?? string.Empty, Latitude = Latitude, Longitude = Longitude, Capacity = Capacity,
            RegistrationStartAt = regStart, RegistrationEndAt = regEnd,
            ImageUrl = string.IsNullOrWhiteSpace(ImageUrl) ? null : ImageUrl.Trim(), Status = EventStatus.Draft
        };
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        return RedirectToPage("/Events/Details", new { id = evt.Id });
    }

    private bool ValidateCoordinates()
    {
        if (Latitude.HasValue != Longitude.HasValue)
        {
            Error = "Укажите широту и долготу вместе либо оставьте оба поля пустыми.";
            return false;
        }
        if (Latitude is < -90 or > 90 || Longitude is < -180 or > 180)
        {
            Error = "Проверьте координаты: широта от -90 до 90, долгота от -180 до 180.";
            return false;
        }
        return true;
    }

    private async Task LoadCommunitiesAsync()
    {
        if (AccessControlService.IsGlobalAdmin(User))
        {
            Communities = await db.Communities.AsNoTracking().OrderBy(x => x.Name).Select(x => new CommunityVm(x.Id, x.Name)).ToListAsync();
        }
        else
        {
            var userId = AccessControlService.GetUserId(User);
            Communities = userId is null ? [] : await db.CommunityMembers.AsNoTracking()
                .Where(x => x.UserId == userId.Value && (x.Role == CommunityMemberRole.Owner || x.Role == CommunityMemberRole.Admin))
                .OrderBy(x => x.Community.Name).Select(x => new CommunityVm(x.CommunityId, x.Community.Name)).ToListAsync();
        }
        if (CommunityId is null && Communities.Count > 0) CommunityId = Communities[0].Id;
    }

    private static bool TryLocal(string? value, out DateTimeOffset result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value) || !DateTime.TryParse(value, out var local)) return false;
        result = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local));
        return true;
    }

    public sealed record CommunityVm(Guid Id, string Name);
}

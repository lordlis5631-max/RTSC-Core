using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Events;

[Authorize]
public sealed class EditModel(AppDbContext db, AccessControlService access) : PageModel
{
    public Guid Id { get; private set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? Place { get; set; }
    [BindProperty] public double? Latitude { get; set; }
    [BindProperty] public double? Longitude { get; set; }
    [BindProperty] public int? Capacity { get; set; }
    [BindProperty] public string StartAtLocal { get; set; } = string.Empty;
    [BindProperty] public string? EndAtLocal { get; set; }
    [BindProperty] public string? RegistrationStartLocal { get; set; }
    [BindProperty] public string? RegistrationEndLocal { get; set; }
    [BindProperty] public string? ImageUrl { get; set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await access.CanManageEventAsync(id, User)) return Forbid();
        var evt = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id); if (evt is null) return NotFound();
        Id = id; Title = evt.Title; Description = evt.Description; Place = evt.Place; Latitude = evt.Latitude; Longitude = evt.Longitude; Capacity = evt.Capacity; ImageUrl = evt.ImageUrl;
        StartAtLocal = evt.StartAt.ToLocalTime().ToString("yyyy-MM-ddTHH:mm"); EndAtLocal = evt.EndAt?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm");
        RegistrationStartLocal = evt.RegistrationStartAt?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm"); RegistrationEndLocal = evt.RegistrationEndAt?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        Id = id; if (!await access.CanManageEventAsync(id, User)) return Forbid();
        if (string.IsNullOrWhiteSpace(Title) || !TryLocal(StartAtLocal, out var start)) { Error = "Заполните название и дату начала."; return Page(); }
        DateTimeOffset? end = TryLocal(EndAtLocal, out var e) ? e : null; DateTimeOffset? rs = TryLocal(RegistrationStartLocal, out var r1) ? r1 : null; DateTimeOffset? re = TryLocal(RegistrationEndLocal, out var r2) ? r2 : null;
        if (end is not null && end <= start) { Error = "Окончание должно быть позже начала."; return Page(); }
        if (Capacity is <= 0) { Error = "Лимит должен быть больше нуля."; return Page(); }
        if (rs is not null && re is not null && re <= rs) { Error = "Проверьте период регистрации."; return Page(); }
        if (!ValidateCoordinates()) return Page();
        var evt = await db.Events.SingleOrDefaultAsync(x => x.Id == id); if (evt is null) return NotFound();
        evt.Title = Title.Trim(); evt.Description = Description?.Trim() ?? string.Empty; evt.Place = Place?.Trim() ?? string.Empty; evt.Latitude = Latitude; evt.Longitude = Longitude; evt.Capacity = Capacity; evt.StartAt = start; evt.EndAt = end; evt.RegistrationStartAt = rs; evt.RegistrationEndAt = re; evt.ImageUrl = string.IsNullOrWhiteSpace(ImageUrl) ? null : ImageUrl.Trim();
        await db.SaveChangesAsync(); return RedirectToPage("/Events/Details", new { id });
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

    private static bool TryLocal(string? value, out DateTimeOffset result) { result = default; if (string.IsNullOrWhiteSpace(value) || !DateTime.TryParse(value, out var local)) return false; result = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local)); return true; }
}

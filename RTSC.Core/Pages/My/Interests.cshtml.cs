using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.Personalization;

namespace RTSC.Core.Pages.My;

[Authorize]
public sealed class InterestsModel(AppDbContext db) : PageModel
{
    [BindProperty] public List<EventCategory> SelectedCategories { get; set; } = [];
    [BindProperty] public List<Guid> SelectedTagIds { get; set; } = [];

    public IReadOnlyList<CategoryVm> Categories { get; private set; } = [];
    public IReadOnlyList<TagVm> Tags { get; private set; } = [];
    [TempData] public string? Message { get; set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AccessControlService.GetUserId(User);
        if (userId is null) return Challenge();

        SelectedCategories = await db.UserCategoryInterests.AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.Category)
            .ToListAsync();

        SelectedTagIds = await db.UserTagInterests.AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.TagId)
            .ToListAsync();

        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = AccessControlService.GetUserId(User);
        if (userId is null) return Challenge();

        SelectedCategories = SelectedCategories.Distinct().ToList();
        SelectedTagIds = SelectedTagIds.Distinct().ToList();

        if (SelectedCategories.Any(x => !Enum.IsDefined(x)))
        {
            Error = "В списке категорий есть неизвестное значение.";
            await LoadOptionsAsync();
            return Page();
        }

        if (SelectedTagIds.Count > 12)
        {
            Error = "Можно выбрать не более 12 тегов интересов.";
            await LoadOptionsAsync();
            return Page();
        }

        var validTagIds = await db.Tags.AsNoTracking()
            .Where(x => x.IsActive && SelectedTagIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        if (validTagIds.Count != SelectedTagIds.Count)
        {
            Error = "Один из выбранных тегов недоступен.";
            await LoadOptionsAsync();
            return Page();
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.UserCategoryInterests.Where(x => x.UserId == userId.Value).ExecuteDeleteAsync();
        await db.UserTagInterests.Where(x => x.UserId == userId.Value).ExecuteDeleteAsync();

        db.UserCategoryInterests.AddRange(SelectedCategories.Select(category => new UserCategoryInterest
        {
            UserId = userId.Value,
            Category = category
        }));

        db.UserTagInterests.AddRange(validTagIds.Select(tagId => new UserTagInterest
        {
            UserId = userId.Value,
            TagId = tagId
        }));

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        Message = "Интересы сохранены. Рекомендации в «Мой RTSC» обновлены.";
        return RedirectToPage();
    }

    private async Task LoadOptionsAsync()
    {
        Categories = EventCategoryCatalog.All
            .Select(x => new CategoryVm(x, EventCategoryCatalog.Label(x)))
            .ToList();

        Tags = await db.Tags.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new TagVm(x.Id, x.Name))
            .ToListAsync();
    }

    public sealed record CategoryVm(EventCategory Value, string Label);
    public sealed record TagVm(Guid Id, string Name);
}

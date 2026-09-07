using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages.Events;

public sealed class CalendarModel(AppDbContext db) : PageModel
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string MonthTitle { get; private set; } = string.Empty;
    public string PreviousQuery { get; private set; } = string.Empty;
    public string NextQuery { get; private set; } = string.Empty;
    public int FirstDayOffset { get; private set; }
    public IReadOnlyList<DayVm> Days { get; private set; } = [];

    public async Task OnGetAsync(int? year, int? month)
    {
        var today = DateTimeOffset.UtcNow;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        if (selectedYear is < 2000 or > 2100 || selectedMonth is < 1 or > 12)
        {
            selectedYear = today.Year;
            selectedMonth = today.Month;
        }

        Year = selectedYear;
        Month = selectedMonth;

        var culture = CultureInfo.GetCultureInfo("ru-RU");
        MonthTitle = culture.TextInfo.ToTitleCase(new DateTime(Year, Month, 1).ToString("MMMM yyyy", culture));

        var current = new DateTime(Year, Month, 1);
        PreviousQuery = ToQuery(current.AddMonths(-1));
        NextQuery = ToQuery(current.AddMonths(1));
        FirstDayOffset = ((int)current.DayOfWeek + 6) % 7;

        var start = new DateTimeOffset(Year, Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);

        var events = await db.Events
            .AsNoTracking()
            .Where(x => x.StartAt >= start && x.StartAt < end &&
                        (x.Status == EventStatus.Published || x.Status == EventStatus.Completed))
            .OrderBy(x => x.StartAt)
            .Select(x => new EventVm(
                x.Id,
                x.Title,
                x.StartAt,
                x.Place,
                x.Community.Name,
                x.Status.ToString()))
            .ToListAsync();

        var byDay = events.GroupBy(x => x.StartAt.UtcDateTime.Day)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<EventVm>)x.ToList());

        Days = Enumerable.Range(1, DateTime.DaysInMonth(Year, Month))
            .Select(day => new DayVm(day, byDay.GetValueOrDefault(day) ?? []))
            .ToList();
    }

    private static string ToQuery(DateTime date) => $"?year={date.Year}&month={date.Month}";

    public sealed record DayVm(int Day, IReadOnlyList<EventVm> Events);
    public sealed record EventVm(Guid Id, string Title, DateTimeOffset StartAt, string Place, string CommunityName, string Status);
}

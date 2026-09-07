using RTSC.Core.Domain;

namespace RTSC.Core.Features.Personalization;

public static class EventCategoryCatalog
{
    public static IReadOnlyList<EventCategory> All { get; } = Enum.GetValues<EventCategory>();

    public static string Label(EventCategory category) => category switch
    {
        EventCategory.Education => "Образование",
        EventCategory.Career => "Карьера",
        EventCategory.Technology => "Технологии",
        EventCategory.Science => "Наука",
        EventCategory.Culture => "Культура",
        EventCategory.Sports => "Спорт",
        EventCategory.Volunteering => "Волонтёрство",
        EventCategory.Games => "Игры",
        EventCategory.Creativity => "Творчество",
        EventCategory.Entrepreneurship => "Предпринимательство",
        EventCategory.Health => "Здоровье",
        _ => "Другое"
    };
}

using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Features.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/{id:guid}/participants.csv", ExportParticipantsAsync)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> ExportParticipantsAsync(
        Guid id,
        ClaimsPrincipal user,
        AccessControlService access,
        AppDbContext db)
    {
        if (!await access.CanManageEventAsync(id, user))
            return Results.Forbid();

        var exists = await db.Events.AsNoTracking().AnyAsync(x => x.Id == id);
        if (!exists)
            return Results.NotFound();

        var participants = await db.EventParticipants
            .AsNoTracking()
            .Where(x => x.EventId == id)
            .OrderBy(x => x.User.DisplayName)
            .Select(x => new
            {
                x.User.DisplayName,
                x.User.Email,
                Status = x.Status.ToString(),
                x.RegisteredAt,
                x.ConfirmedAt,
                x.AttendedAt
            })
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Имя;Email;Статус;Дата регистрации;Дата подтверждения;Дата посещения");

        foreach (var item in participants)
        {
            csv.Append(CsvCell(item.DisplayName)).Append(';')
                .Append(CsvCell(item.Email)).Append(';')
                .Append(CsvCell(item.Status)).Append(';')
                .Append(CsvCell(FormatDate(item.RegisteredAt))).Append(';')
                .Append(CsvCell(FormatDate(item.ConfirmedAt))).Append(';')
                .Append(CsvCell(FormatDate(item.AttendedAt))).AppendLine();
        }

        var body = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        return Results.File(
            body,
            "text/csv; charset=utf-8",
            $"participants-{id:N}.csv");
    }

    private static string FormatDate(DateTimeOffset? value) =>
        value?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? string.Empty;

    private static string CsvCell(string? value)
    {
        var text = value ?? string.Empty;

        // Prevent spreadsheet formula injection when the CSV is opened in Excel/LibreOffice.
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
            text = "'" + text;

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}

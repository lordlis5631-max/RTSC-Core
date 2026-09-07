using Microsoft.EntityFrameworkCore;
using QRCoder;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Features.CheckIn;

public static class CheckInEndpoints
{
    public static IEndpointRouteBuilder MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/checkin").WithTags("Check-in").RequireAuthorization();

        group.MapGet("/ticket/{eventId:guid}/qr", async (Guid eventId, HttpContext http, AppDbContext db, CheckInTokenService tokens) =>
        {
            var userId = AccessControlService.GetUserId(http.User);
            if (userId is null) return Results.Unauthorized();

            var participant = await db.EventParticipants.AsNoTracking()
                .SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId.Value && x.Status != ParticipantStatus.Cancelled);
            if (participant is null) return Results.NotFound(new { error = "registration not found" });

            var token = tokens.Create(eventId, userId.Value, TimeSpan.FromDays(14));
            var link = $"{http.Request.Scheme}://{http.Request.Host}/CheckIn?token={Uri.EscapeDataString(token)}";

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(link, QRCodeGenerator.ECCLevel.Q);
            var svg = new SvgQRCode(data).GetGraphic(6);
            return Results.Text(svg, "image/svg+xml; charset=utf-8");
        });

        return app;
    }
}

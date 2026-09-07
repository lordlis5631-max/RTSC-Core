using Microsoft.AspNetCore.DataProtection;

namespace RTSC.Core.Features.CheckIn;

public sealed class CheckInTokenService(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("RTSC.Core.CheckIn.v1");

    public string Create(Guid eventId, Guid userId, TimeSpan lifetime)
    {
        var expires = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        return _protector.Protect($"{eventId:N}|{userId:N}|{expires}");
    }

    public bool TryRead(string token, out CheckInTicket ticket)
    {
        ticket = default!;
        try
        {
            var raw = _protector.Unprotect(token);
            var parts = raw.Split('|');
            if (parts.Length != 3 ||
                !Guid.TryParseExact(parts[0], "N", out var eventId) ||
                !Guid.TryParseExact(parts[1], "N", out var userId) ||
                !long.TryParse(parts[2], out var expires))
                return false;

            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expires);
            if (expiresAt <= DateTimeOffset.UtcNow) return false;
            ticket = new CheckInTicket(eventId, userId, expiresAt);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public sealed record CheckInTicket(Guid EventId, Guid UserId, DateTimeOffset ExpiresAt);
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Features.ExternalAccounts;

public sealed class ExternalLinkService(AppDbContext db)
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    public async Task<LinkCode> CreateAsync(Guid userId, ExternalProvider provider, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await db.ExternalLinkTokens
            .Where(x => x.ExpiresAt < now.AddDays(-1))
            .ExecuteDeleteAsync(cancellationToken);

        var oldTokens = await db.ExternalLinkTokens
            .Where(x => x.UserId == userId && x.Provider == provider && x.UsedAt == null)
            .ToListAsync(cancellationToken);

        if (oldTokens.Count > 0)
            db.ExternalLinkTokens.RemoveRange(oldTokens);

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
        var token = new ExternalLinkToken
        {
            UserId = userId,
            Provider = provider,
            TokenHash = Hash(code),
            ExpiresAt = now.Add(LinkLifetime)
        };

        db.ExternalLinkTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
        return new LinkCode(code, token.ExpiresAt);
    }

    public async Task<RedeemResult> RedeemAsync(
        ExternalProvider provider,
        string code,
        string externalUserId,
        string? username,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(code);
        var normalizedExternalId = externalUserId.Trim();
        if (normalizedCode.Length == 0 || normalizedExternalId.Length == 0)
            return RedeemResult.Invalid();

        var now = DateTimeOffset.UtcNow;
        var tokenHash = Hash(normalizedCode);

        var token = await db.ExternalLinkTokens
            .SingleOrDefaultAsync(x =>
                x.Provider == provider &&
                x.TokenHash == tokenHash &&
                x.UsedAt == null &&
                x.ExpiresAt > now, cancellationToken);

        if (token is null)
            return RedeemResult.Invalid();

        var accountByExternalId = await db.ExternalAccounts
            .SingleOrDefaultAsync(x => x.Provider == provider && x.ExternalUserId == normalizedExternalId, cancellationToken);

        if (accountByExternalId is not null && accountByExternalId.UserId != token.UserId)
            return RedeemResult.AlreadyLinked();

        var account = await db.ExternalAccounts
            .SingleOrDefaultAsync(x => x.UserId == token.UserId && x.Provider == provider, cancellationToken);

        if (account is null)
        {
            account = new ExternalAccount
            {
                UserId = token.UserId,
                Provider = provider,
                ExternalUserId = normalizedExternalId,
                Username = NormalizeOptional(username),
                LastSeenAt = now,
                NotificationsEnabled = true
            };
            db.ExternalAccounts.Add(account);
        }
        else
        {
            account.ExternalUserId = normalizedExternalId;
            account.Username = NormalizeOptional(username);
            account.LastSeenAt = now;
        }

        token.UsedAt = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return RedeemResult.AlreadyLinked();
        }

        return RedeemResult.Success(token.UserId);
    }

    public async Task TouchAsync(
        ExternalProvider provider,
        string externalUserId,
        string? username,
        CancellationToken cancellationToken = default)
    {
        var account = await db.ExternalAccounts
            .SingleOrDefaultAsync(x => x.Provider == provider && x.ExternalUserId == externalUserId, cancellationToken);

        if (account is null) return;

        account.LastSeenAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(username))
            account.Username = username.Trim();

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeCode(string value) =>
        value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeCode(value)));
        return Convert.ToHexString(bytes);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record LinkCode(string Code, DateTimeOffset ExpiresAt);

    public sealed record RedeemResult(bool IsSuccess, Guid? UserId, string? Error)
    {
        public static RedeemResult Success(Guid userId) => new(true, userId, null);
        public static RedeemResult Invalid() => new(false, null, "invalid_or_expired_code");
        public static RedeemResult AlreadyLinked() => new(false, null, "external_account_already_linked");
    }
}

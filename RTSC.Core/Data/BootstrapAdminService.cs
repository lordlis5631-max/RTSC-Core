using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Domain;

namespace RTSC.Core.Data;

public sealed class BootstrapAdminService(IServiceProvider services, IConfiguration configuration, ILogger<BootstrapAdminService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];
        var displayName = configuration["BootstrapAdmin:DisplayName"]?.Trim() ?? "Super Admin";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var existing = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role != GlobalRole.SuperAdmin)
            {
                existing.Role = GlobalRole.SuperAdmin;
                await db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var admin = new User
        {
            DisplayName = displayName,
            Email = email,
            Role = GlobalRole.SuperAdmin,
            Status = UserStatus.Active
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Bootstrap SuperAdmin created for {Email}", email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

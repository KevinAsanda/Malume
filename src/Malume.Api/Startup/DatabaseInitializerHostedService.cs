using Malume.Application.Abstractions.Identity;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Malume.Api.Startup;

internal sealed class DatabaseInitializerHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IConfiguration _configuration = configuration;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        MalumeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MalumeDbContext>();

        if (_configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        string? adminEmail = _configuration["Bootstrap:SysAdminEmail"];
        string? adminPassword = _configuration["Bootstrap:SysAdminPassword"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            string normalizedEmail = adminEmail.Trim().ToUpperInvariant();
            bool exists = await dbContext.Users
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (!exists)
            {
                IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                string fullName = _configuration["Bootstrap:SysAdminFullName"] ?? "Malume System Administrator";

                var sysAdmin = new User(
                    adminEmail,
                    fullName,
                    "+27000000000",
                    hasher.HashPassword(adminPassword),
                    UserRole.SysAdmin);

                await dbContext.Users.AddAsync(sysAdmin, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

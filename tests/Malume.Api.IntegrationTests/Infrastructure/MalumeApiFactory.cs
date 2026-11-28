using Malume.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Malume.Api.IntegrationTests.Infrastructure;

public sealed class MalumeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestJwtSigningKey = "Malume_Integration_Test_Signing_Key_Minimum_32_Bytes!";

    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _sqlContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:SigningKey", TestJwtSigningKey);
        builder.UseSetting("Jwt:Issuer", "Malume.Api");
        builder.UseSetting("Jwt:Audience", "Malume.Clients");
        builder.UseSetting("Hangfire:Enabled", "false");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Bootstrap:SysAdminEmail", "sysadmin@malume.test");
        builder.UseSetting("Bootstrap:SysAdminPassword", "MalumeAdmin!2026");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Jwt:SigningKey"] = TestJwtSigningKey,
                ["Jwt:Issuer"] = "Malume.Api",
                ["Jwt:Audience"] = "Malume.Clients",
                ["Hangfire:Enabled"] = "false",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Bootstrap:SysAdminEmail"] = "sysadmin@malume.test",
                ["Bootstrap:SysAdminPassword"] = "MalumeAdmin!2026"
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        MalumeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MalumeDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}

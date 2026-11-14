using Malume.Application.Abstractions.Persistence;
using Malume.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Malume.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<MalumeDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql =>
                {
                    sql.UseNetTopologySuite();
                    sql.MigrationsAssembly(typeof(MalumeDbContext).Assembly.FullName);
                }));

        services.AddScoped<IMalumeDbContext>(sp => sp.GetRequiredService<MalumeDbContext>());

        return services;
    }
}

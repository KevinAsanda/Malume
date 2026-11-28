using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Payments;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Abstractions.Security;
using Malume.Infrastructure.Identity;
using Malume.Infrastructure.Payments;
using Malume.Infrastructure.Persistence;
using Malume.Infrastructure.Persistence.Queries;
using Malume.Infrastructure.Persistence.Repositories;
using Malume.Infrastructure.Security;
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
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<QrSigningOptions>(configuration.GetSection(QrSigningOptions.SectionName));
        services.Configure<PaystackOptions>(configuration.GetSection(PaystackOptions.SectionName));

        services.AddDbContext<MalumeDbContext>((serviceProvider, options) =>
        {
            IConfiguration resolvedConfiguration = serviceProvider.GetService<IConfiguration>() ?? configuration;
            string connectionString = resolvedConfiguration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

            options.UseSqlServer(
                connectionString,
                sql =>
                {
                    sql.UseNetTopologySuite();
                    sql.MigrationsAssembly(typeof(MalumeDbContext).Assembly.FullName);
                });
        });

        services.AddScoped<IMalumeDbContext>(sp => sp.GetRequiredService<MalumeDbContext>());
        services.AddScoped<ISpatialCorridorQueryService, SpatialCorridorQueryService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IQrBadgeSigner, EcdsaQrBadgeSigner>();
        services.AddHttpClient<IPaystackGateway, PaystackHttpGateway>();

        return services;
    }
}

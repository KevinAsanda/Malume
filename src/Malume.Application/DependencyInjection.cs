using FluentValidation;
using Malume.Application.Abstractions.Jobs;
using Malume.Application.Abstractions.Realtime;
using Malume.Application.Common.Behaviors;
using Malume.Application.Features.Compliance;
using Malume.Application.Features.Escrow;
using Malume.Application.Features.Trips;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Malume.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ITripRealtimeNotifier, NullTripRealtimeNotifier>();
        services.AddScoped<EscrowFreezeService>();
        services.AddScoped<IComplianceExpiryScanner, ComplianceExpiryScanner>();
        services.AddScoped<IEscrowPayoutJob, EscrowPayoutProcessor>();

        return services;
    }
}

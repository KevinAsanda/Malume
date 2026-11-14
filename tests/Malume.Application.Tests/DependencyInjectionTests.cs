using Malume.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Malume.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ShouldRegisterMediator()
    {
        ServiceCollection services = new();

        services.AddApplication();

        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IMediator));
    }
}

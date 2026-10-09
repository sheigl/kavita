using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kavita.Server.Tests.DI;

/// <summary>
/// Verifies every registered service can be resolved from the DI container.
/// Catches "missing DI registration" bugs before runtime (e.g., injecting IDataProtector 
/// instead of IDataProtectionProvider). Unit tests that construct services via `new` bypass 
/// the container and won't catch these issues.
/// </summary>
[Collection("Service Resolution")]
public class ServiceResolutionTests
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceResolutionTests(ServiceResolutionFixture fixture)
    {
        _serviceProvider = fixture.ServiceProvider;
    }

    [Theory]
    [MemberData(nameof(GetServicesToVerify))]
    public void AllRegisteredServices_ShouldResolveFromContainer(Type serviceType)
    {
        // Create a scope (matching real request lifecycle for scoped services)
        using var scope = _serviceProvider.CreateScope();

        // This will throw InvalidOperationException if the type or any of its 
        // constructor dependencies cannot be resolved from the container
        var service = scope.ServiceProvider.GetRequiredService(serviceType);
        Assert.NotNull(service);
    }

    public static TheoryData<Type> GetServicesToVerify() => new(ServiceResolutionFixture.ServicesToVerify);
}

/// <summary>
/// Collection definition for Service Resolution tests.
/// Ensures the fixture is shared across all test cases in this collection.
/// </summary>
[CollectionDefinition("Service Resolution")]
public class ServiceResolutionCollection : ICollectionFixture<ServiceResolutionFixture> { }

using Microsoft.Extensions.DependencyInjection;
using StateleSSE.AspNetCore.Infrastructure;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Extension methods for configuring InMemoryBackplane SSE services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds in-memory SSE backplane to the service collection.
    /// Suitable for single-server deployments, development, and testing.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInMemorySseBackplane(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryBackplane>();
        services.AddSingleton<ISseBackplane>(sp => sp.GetRequiredService<InMemoryBackplane>());
        return services;
    }
}

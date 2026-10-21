using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Rayforge.Web;

/// <summary>Registration entry point for a Blazor WebAssembly host.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the game and the services <c>RayforgeCanvas</c> needs. The game is a singleton:
    /// a raylib context cannot be created twice in one page session.
    /// </summary>
    /// <typeparam name="TGame">The game to boot.</typeparam>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configure">Optional configuration applied before the options are frozen into DI.</param>
    public static IServiceCollection AddRayforge<TGame>(
        this IServiceCollection services,
        Action<GameOptions>? configure = null)
        where TGame : class, IGame, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new GameOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton<IGameLog>(ConsoleGameLog.Instance);
        services.TryAddSingleton<TGame>();
        services.TryAddSingleton<IGame>(sp => sp.GetRequiredService<TGame>());

        // Blazor does not register HttpClient; the canvas uses it for the asset manifest.
        services.TryAddScoped(static sp => new HttpClient
        {
            BaseAddress = new Uri(sp.GetRequiredService<NavigationManager>().BaseUri),
        });

        return services;
    }
}

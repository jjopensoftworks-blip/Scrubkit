using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Scrubkit;

/// <summary>
/// Registers Scrubkit in a <see cref="IServiceCollection"/> so a configured
/// <see cref="FolderScrubber"/> can be constructor-injected into ASP.NET, worker, or any
/// other host that uses <c>Microsoft.Extensions.DependencyInjection</c>.
/// </summary>
public static class ScrubkitServiceCollectionExtensions
{
    /// <summary>
    /// Adds a singleton <see cref="FolderScrubber"/> to <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional hook to configure the <see cref="ReadOptions"/> — set recursion, limits,
    /// register add-on <see cref="ReadOptions.Extractors"/>, or supply a
    /// <see cref="ReadOptions.Redactor"/>.
    /// </param>
    public static IServiceCollection AddScrubkit(
        this IServiceCollection services, Action<ReadOptions>? configure = null)
        => services.AddScrubkit((_, options) => configure?.Invoke(options));

    /// <summary>
    /// Adds a singleton <see cref="FolderScrubber"/> whose <see cref="ReadOptions"/> is
    /// configured with access to the <see cref="IServiceProvider"/> — use this to pull a
    /// DI-registered <see cref="IRedactor"/> or <see cref="IFileExtractor"/> into the options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the options, with the built service provider.</param>
    public static IServiceCollection AddScrubkit(
        this IServiceCollection services, Action<IServiceProvider, ReadOptions> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        services.TryAddSingleton(serviceProvider =>
        {
            var options = new ReadOptions();
            configure(serviceProvider, options);
            return new FolderScrubber(options);
        });
        return services;
    }
}

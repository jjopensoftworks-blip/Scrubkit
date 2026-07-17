using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class OfflineGuaranteeTests
{
    // Assemblies whose presence would mean outbound network capability. Scrubkit is offline
    // by design, so neither shipping assembly may reference any of these directly.
    private static readonly HashSet<string> NetworkAssemblies = new(StringComparer.Ordinal)
    {
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.Requests",
        "System.Net.WebClient",
        "System.Net.WebSockets",
        "System.Net.WebSockets.Client",
        "System.Net.Mail",
        "System.Net.NameResolution",
        "System.Net.NetworkInformation",
        "System.Net.Ping",
        "System.Net.Security",
    };

    [Fact]
    public void Shipping_assemblies_reference_nothing_that_talks_to_the_network()
    {
        var assemblies = new[]
        {
            typeof(FolderScrubber).Assembly,   // Scrubkit
            typeof(IFileExtractor).Assembly,   // Scrubkit.Abstractions
        };

        foreach (var asm in assemblies)
        {
            var offenders = asm.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(name => name is not null && NetworkAssemblies.Contains(name))
                .ToList();

            Assert.True(
                offenders.Count == 0,
                $"{asm.GetName().Name} references networking assemblies: {string.Join(", ", offenders)}");
        }
    }
}

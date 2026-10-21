using System.Reflection;
using Rayforge.Desktop;
using Rayforge.Web;
using Xunit;

namespace Rayforge.Tests;

/// <summary>
/// Enforces the dependency rules from docs/PLAN.md 4.5 and docs/ARCHITECTURE.md:
/// Core is platform-neutral, games do not know about hosts, hosts do not depend on each other.
/// </summary>
public class DependencyRuleTests
{
    private static readonly Assembly CoreAssembly = typeof(IGame).Assembly;
    private static readonly Assembly WebAssembly = typeof(ServiceCollectionExtensions).Assembly;
    private static readonly Assembly DesktopAssembly = typeof(DesktopRunner).Assembly;
    private static readonly Assembly GameAssembly = typeof(HelloWorld.HelloWorldGame).Assembly;

    private static readonly string[] ForbiddenInCore =
    [
        "Microsoft.AspNetCore",
        "Microsoft.JSInterop",
        "System.Runtime.InteropServices.JavaScript",
        "Rayforge.Web",
        "Rayforge.Desktop",
    ];

    private static readonly string[] ForbiddenInGame =
    [
        "Microsoft.AspNetCore",
        "Microsoft.JSInterop",
        "System.Runtime.InteropServices.JavaScript",
        "Rayforge.Web",
        "Rayforge.Desktop",
    ];

    [Fact]
    public void Core_does_not_reference_blazor_js_interop_or_hosts()
    {
        AssertNoReference(CoreAssembly, ForbiddenInCore);
    }

    [Fact]
    public void Core_references_raylib_cs_only_as_a_drawing_library()
    {
        Assert.Contains("Raylib-cs", References(CoreAssembly));
    }

    [Fact]
    public void Game_does_not_reference_hosts()
    {
        AssertNoReference(GameAssembly, ForbiddenInGame);
    }

    [Fact]
    public void Game_references_core_and_raylib()
    {
        string[] references = References(GameAssembly);

        Assert.Contains("Rayforge.Core", references);
        Assert.Contains("Raylib-cs", references);
    }

    [Fact]
    public void Web_host_references_core_but_not_the_desktop_host()
    {
        string[] references = References(WebAssembly);

        Assert.Contains("Rayforge.Core", references);
        Assert.DoesNotContain("Rayforge.Desktop", references);
    }

    [Fact]
    public void Desktop_host_references_core_but_not_the_web_host()
    {
        string[] references = References(DesktopAssembly);

        Assert.Contains("Rayforge.Core", references);
        Assert.DoesNotContain("Rayforge.Web", references);
    }

    private static string[] References(Assembly assembly)
        => assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

    private static void AssertNoReference(Assembly assembly, string[] forbiddenPrefixes)
    {
        string[] references = References(assembly);

        string[] violations = references
            .Where(reference => forbiddenPrefixes.Any(prefix =>
                reference.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                reference.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"{assembly.GetName().Name} must not reference: {string.Join(", ", violations)}");
    }
}

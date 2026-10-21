using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Rayforge.Web;

/// <summary>
/// JSImport declarations for <c>wwwroot/rayforge.js</c>. The module is imported once by
/// <c>RayforgeCanvas</c> before any of these members are called.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class RayforgeInterop
{
    /// <summary>Module name used for both <c>JSHost.ImportAsync</c> and <c>[JSImport]</c>.</summary>
    internal const string ModuleName = "rayforge";

    /// <summary>Binds the canvas and returns <c>{ cssWidth, cssHeight, dpr }</c>.</summary>
    [JSImport("init", ModuleName)]
    internal static partial JSObject Init(string canvasId);

    /// <summary>Applies the letterbox size in CSS pixels. The backing store stays owned by C#.</summary>
    [JSImport("setCanvasCssSize", ModuleName)]
    internal static partial void SetCanvasCssSize(string canvasId, double cssWidth, double cssHeight);

    /// <summary>Releases the canvas binding.</summary>
    [JSImport("dispose", ModuleName)]
    internal static partial void Dispose(string canvasId);
}

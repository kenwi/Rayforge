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
    internal static partial JSObject Init(string canvasId, string mode, string title);

    /// <summary>Fetches every path into the Emscripten MEMFS. Returns the number of bytes written.</summary>
    [JSImport("preload", ModuleName)]
    internal static partial Task<double> Preload(string[] paths);

    /// <summary>Resolves the C# exports and starts the requestAnimationFrame loop.</summary>
    [JSImport("start", ModuleName)]
    internal static partial Task Start([JSMarshalAs<JSType.Any>] object handle, string assemblyName, int targetFps);

    /// <summary>Stops the loop. Safe to call repeatedly.</summary>
    [JSImport("stop", ModuleName)]
    internal static partial void Stop();

    /// <summary>Applies the letterboxed size in CSS pixels. The backing store stays owned by C#.</summary>
    [JSImport("setCanvasCssSize", ModuleName)]
    internal static partial void SetCanvasCssSize(double cssWidth, double cssHeight);

    /// <summary>Applies a new backing-store size (Fill mode only).</summary>
    [JSImport("setBackingSize", ModuleName)]
    internal static partial void SetBackingSize(int width, int height);

    /// <summary>Toggles DOM fullscreen on the canvas container. Needs a user gesture.</summary>
    [JSImport("toggleFullscreen", ModuleName)]
    internal static partial Task<bool> ToggleFullscreen();

    /// <summary>Requests pointer lock. The browser rejects this outside a user gesture.</summary>
    [JSImport("requestPointerLock", ModuleName)]
    internal static partial void RequestPointerLock();

    /// <summary>Exits pointer lock, if held.</summary>
    [JSImport("releasePointerLock", ModuleName)]
    internal static partial void ReleasePointerLock();

    /// <summary>Returns accumulated pointer-lock movement and clears it.</summary>
    [JSImport("consumePointerDelta", ModuleName)]
    internal static partial JSObject ConsumePointerDelta();

    /// <summary>Reloads the page. The only safe way to get a fresh raylib context.</summary>
    [JSImport("reload", ModuleName)]
    internal static partial void Reload();

    /// <summary>Stops the loop and releases the canvas binding.</summary>
    [JSImport("dispose", ModuleName)]
    internal static partial void Dispose();
}

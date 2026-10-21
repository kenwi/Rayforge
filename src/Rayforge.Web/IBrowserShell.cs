using System.Runtime.Versioning;

namespace Rayforge.Web;

/// <summary>Host operations the canvas component exposes to <see cref="BrowserRuntimeHost"/>.</summary>
[SupportedOSPlatform("browser")]
internal interface IBrowserShell
{
    bool IsFullscreen { get; }

    void RequestQuit();

    void ToggleFullscreen();
}

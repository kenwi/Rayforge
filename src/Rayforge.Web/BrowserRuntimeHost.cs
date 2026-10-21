using System.Runtime.Versioning;

namespace Rayforge.Web;

/// <summary>
/// Browser implementation of <see cref="IRuntimeHost"/>: quit stops the rAF loop, fullscreen uses the
/// DOM Fullscreen API (needs a user gesture).
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserRuntimeHost : IRuntimeHost
{
    private readonly IBrowserShell _shell;

    internal BrowserRuntimeHost(IBrowserShell shell) => _shell = shell;

    /// <inheritdoc />
    public bool IsFullscreen => _shell.IsFullscreen;

    /// <inheritdoc />
    public void RequestQuit() => _shell.RequestQuit();

    /// <inheritdoc />
    public void ToggleFullscreen() => _shell.ToggleFullscreen();
}

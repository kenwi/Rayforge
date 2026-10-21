namespace Rayforge.Desktop;

/// <summary>Window operations for the native host.</summary>
public sealed class DesktopRuntimeHost : IRuntimeHost
{
    private bool _quitRequested;

    /// <summary>Set by <see cref="RequestQuit"/>; the runner checks it every frame.</summary>
    public bool QuitRequested => _quitRequested;

    /// <inheritdoc />
    public bool IsFullscreen { get; private set; }

    /// <inheritdoc />
    public void RequestQuit() => _quitRequested = true;

    /// <inheritdoc />
    public void ToggleFullscreen()
    {
        Raylib_cs.Raylib.ToggleFullscreen();
        IsFullscreen = Raylib_cs.Raylib.IsWindowFullscreen();
    }
}

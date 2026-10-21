namespace Rayforge;

/// <summary>Window operations that need host-specific code.</summary>
public interface IRuntimeHost
{
    /// <summary>True while the canvas/window is fullscreen.</summary>
    bool IsFullscreen { get; }

    /// <summary>Ask the host to stop the loop and tear down. Never call <c>Environment.Exit</c>.</summary>
    void RequestQuit();

    /// <summary>Toggle fullscreen. On the web this needs a user gesture.</summary>
    void ToggleFullscreen();
}

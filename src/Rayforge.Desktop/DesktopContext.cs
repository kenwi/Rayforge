namespace Rayforge.Desktop;

/// <summary>Builds the <see cref="GameContext"/> for a native run.</summary>
internal static class DesktopContext
{
    internal static GameContext Create(
        GameOptions options,
        DesktopRuntimeHost host,
        DesktopMouseCapture mouse,
        GameDiagnostics diagnostics,
        IGameLog log,
        bool audioAvailable)
        => new()
        {
            Width = options.Width,
            Height = options.Height,
            IsBrowser = false,
            AudioAvailable = audioAvailable,
            Runtime = host,
            MouseCapture = mouse,
            Log = log,
            Diagnostics = diagnostics,
        };
}

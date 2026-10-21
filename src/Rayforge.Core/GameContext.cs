namespace Rayforge;

/// <summary>
/// Platform services handed to a game at <see cref="IGame.Initialize"/>. Everything the browser and the
/// desktop implement differently lives here; rendering and input remain plain raylib calls.
/// </summary>
public sealed class GameContext
{
    /// <summary>Logical width at initialization (the canvas backing width in Fit mode).</summary>
    public required int Width { get; init; }

    /// <summary>Logical height at initialization (the canvas backing height in Fit mode).</summary>
    public required int Height { get; init; }

    /// <summary>True when running in the browser. Use it for shader variants and capability checks.</summary>
    public required bool IsBrowser { get; init; }

    /// <summary>False when audio initialization was skipped or failed. Guard sound loading.</summary>
    public required bool AudioAvailable { get; init; }

    /// <summary>Quit and fullscreen - the two window operations that differ per host.</summary>
    public required IRuntimeHost Runtime { get; init; }

    /// <summary>Pointer lock / cursor capture. On the web, <see cref="IMouseCapture.Request"/> needs a gesture.</summary>
    public required IMouseCapture MouseCapture { get; init; }

    /// <summary>Host log (browser console / stdout). Prefer the diagnostics overlay for per-frame data.</summary>
    public required IGameLog Log { get; init; }

    /// <summary>Read-only counters maintained by the host. Never render these per frame through Blazor.</summary>
    public required GameDiagnostics Diagnostics { get; init; }
}

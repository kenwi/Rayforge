using Raylib_cs;

namespace Rayforge;

/// <summary>
/// Host configuration for one game run. Set by the host (web or desktop); the game reads it through
/// <see cref="GameContext"/> instead of re-reading the environment.
/// </summary>
public sealed class GameOptions
{
    /// <summary>Window title (desktop) and <c>document.title</c> (web).</summary>
    public string Title { get; set; } = "Rayforge Game";

    /// <summary>Logical width. In <see cref="CanvasScaleMode.Fit"/> this is the fixed canvas backing width.</summary>
    public int Width { get; set; } = 1280;

    /// <summary>Logical height. In <see cref="CanvasScaleMode.Fit"/> this is the fixed canvas backing height.</summary>
    public int Height { get; set; } = 720;

    /// <summary>How the canvas maps the logical size onto the viewport. Fit is the default.</summary>
    public CanvasScaleMode ScaleMode { get; set; } = CanvasScaleMode.Fit;

    /// <summary>How the web host discovers assets. Both modes fetch into MEMFS; only the list differs.</summary>
    public AssetMode AssetMode { get; set; } = AssetMode.Bundled;

    /// <summary>Initialize the audio device. On the web this must happen after a user gesture.</summary>
    public bool EnableAudio { get; set; } = true;

    /// <summary>Show "Click to play" before starting. Needed for reliable audio on the web.</summary>
    public bool RequireUserGesture { get; set; } = true;

    /// <summary>Frame cap; 0 means the host's native pacing (rAF on web, vsync on desktop).</summary>
    public int TargetFps { get; set; }

    /// <summary>
    /// Applied before <c>InitWindow</c> on desktop. The browser does not honor raylib window flags;
    /// do not rely on them for cursor or fullscreen behavior on the web.
    /// </summary>
    public ConfigFlags ConfigFlags { get; set; } = ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint;
}

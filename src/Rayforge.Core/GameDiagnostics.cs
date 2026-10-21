namespace Rayforge;

/// <summary>
/// Read-only counters written by the host, safe for a debug overlay. The host updates them about once per
/// second; games must not drive Blazor renders from here.
/// </summary>
public sealed class GameDiagnostics
{
    /// <summary>Frames per second measured by the host (not raylib's busy-wait counter on web).</summary>
    public double Fps { get; set; }

    /// <summary>Last frame time in milliseconds.</summary>
    public double FrameTimeMilliseconds { get; set; }

    /// <summary>Canvas backing width in pixels.</summary>
    public int BackingWidth { get; set; }

    /// <summary>Canvas backing height in pixels.</summary>
    public int BackingHeight { get; set; }

    /// <summary>Canvas CSS width in pixels.</summary>
    public int CssWidth { get; set; }

    /// <summary>Canvas CSS height in pixels.</summary>
    public int CssHeight { get; set; }

    /// <summary>Backing/CSS ratio applied to mouse coordinates (X).</summary>
    public float MouseScaleX { get; set; } = 1f;

    /// <summary>Backing/CSS ratio applied to mouse coordinates (Y).</summary>
    public float MouseScaleY { get; set; } = 1f;

    /// <summary>Which asset strategy the host used.</summary>
    public AssetMode AssetMode { get; set; }

    /// <summary>Short human-readable state ("loading assets", "running", ...).</summary>
    public string Status { get; set; } = "created";
}

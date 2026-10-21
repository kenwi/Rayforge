namespace Rayforge;

/// <summary>How the logical resolution is mapped onto the canvas.</summary>
public enum CanvasScaleMode
{
    /// <summary>
    /// Fixed logical resolution, aspect preserved, letterboxed. The game sees a stable size and the host
    /// maps CSS pixels to backing pixels. Best default for ports.
    /// </summary>
    Fit,

    /// <summary>
    /// Backing store follows the CSS size; the game handles arbitrary dimensions via
    /// <c>GetScreenWidth/Height</c> and <see cref="IGame.OnResize"/>.
    /// </summary>
    Fill,
}

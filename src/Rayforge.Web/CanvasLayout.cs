namespace Rayforge.Web;

/// <summary>
/// Pure canvas geometry. No JS, no Blazor, no raylib - everything here is unit-tested.
/// </summary>
/// <remarks>
/// <para><b>Fit:</b> the backing store stays at the logical size; the canvas is letterboxed in CSS and the
/// mouse scale maps CSS pixels back to backing pixels.</para>
/// <para><b>Fill:</b> the backing store follows the CSS size (DPR 1 in v1); mouse coordinates are 1:1.</para>
/// HiDPI is a separate future mode; it must not be mixed into these two.
/// </remarks>
public readonly record struct CanvasLayout
{
    /// <summary>Canvas CSS width in pixels.</summary>
    public required double CssWidth { get; init; }

    /// <summary>Canvas CSS height in pixels.</summary>
    public required double CssHeight { get; init; }

    /// <summary>Horizontal letterbox offset inside the host, in CSS pixels.</summary>
    public required double OffsetX { get; init; }

    /// <summary>Vertical letterbox offset inside the host, in CSS pixels.</summary>
    public required double OffsetY { get; init; }

    /// <summary>Backing/CSS ratio applied to mouse X, or 1 in Fill mode.</summary>
    public required double MouseScaleX { get; init; }

    /// <summary>Backing/CSS ratio applied to mouse Y, or 1 in Fill mode.</summary>
    public required double MouseScaleY { get; init; }

    /// <summary>The uniform scale applied to the logical size, or 1 in Fill mode.</summary>
    public required double Scale { get; init; }

    /// <summary>
    /// Letterboxes a fixed logical resolution inside the viewport, preserving aspect ratio.
    /// Canvas backing size stays <paramref name="logicalWidth"/> x <paramref name="logicalHeight"/>.
    /// </summary>
    /// <param name="logicalWidth">Backing width; must be positive.</param>
    /// <param name="logicalHeight">Backing height; must be positive.</param>
    /// <param name="viewportWidth">Available CSS width. Non-positive means "not measured yet".</param>
    /// <param name="viewportHeight">Available CSS height. Non-positive means "not measured yet".</param>
    /// <param name="integerScaling">Round down to whole multiples when the viewport is at least 1:1.</param>
    public static CanvasLayout Fit(
        int logicalWidth,
        int logicalHeight,
        double viewportWidth,
        double viewportHeight,
        bool integerScaling = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalHeight);

        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return Empty(viewportWidth, viewportHeight);
        }

        double scale = Math.Min(viewportWidth / logicalWidth, viewportHeight / logicalHeight);

        if (integerScaling && scale >= 1)
        {
            scale = Math.Floor(scale);
        }

        double cssWidth = logicalWidth * scale;
        double cssHeight = logicalHeight * scale;

        return new CanvasLayout
        {
            CssWidth = cssWidth,
            CssHeight = cssHeight,
            OffsetX = (viewportWidth - cssWidth) / 2,
            OffsetY = (viewportHeight - cssHeight) / 2,
            // backing / css, which is 1 / scale for a uniform fit.
            MouseScaleX = logicalWidth / cssWidth,
            MouseScaleY = logicalHeight / cssHeight,
            Scale = scale,
        };
    }

    /// <summary>
    /// Fills the viewport: backing = CSS size (DPR 1), no offsets, 1:1 mouse.
    /// The game must handle arbitrary sizes through <see cref="IGame.OnResize"/>.
    /// </summary>
    public static CanvasLayout Fill(double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return Empty(viewportWidth, viewportHeight);
        }

        return new CanvasLayout
        {
            CssWidth = viewportWidth,
            CssHeight = viewportHeight,
            OffsetX = 0,
            OffsetY = 0,
            MouseScaleX = 1,
            MouseScaleY = 1,
            Scale = 1,
        };
    }

    private static CanvasLayout Empty(double viewportWidth, double viewportHeight) => new()
    {
        CssWidth = Math.Max(0, viewportWidth),
        CssHeight = Math.Max(0, viewportHeight),
        OffsetX = 0,
        OffsetY = 0,
        MouseScaleX = 1,
        MouseScaleY = 1,
        Scale = 0,
    };
}

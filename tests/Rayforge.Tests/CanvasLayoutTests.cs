using Rayforge.Web;
using Xunit;

namespace Rayforge.Tests;

public class CanvasLayoutTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Fit_matching_aspect_fills_viewport()
    {
        CanvasLayout layout = CanvasLayout.Fit(1280, 720, 1280, 720);

        Assert.Equal(1, layout.Scale, Tolerance);
        Assert.Equal(1280, layout.CssWidth, Tolerance);
        Assert.Equal(720, layout.CssHeight, Tolerance);
        Assert.Equal(0, layout.OffsetX, Tolerance);
        Assert.Equal(0, layout.OffsetY, Tolerance);
        Assert.Equal(1, layout.MouseScaleX, Tolerance);
        Assert.Equal(1, layout.MouseScaleY, Tolerance);
    }

    [Fact]
    public void Fit_tall_viewport_letterboxes_vertically()
    {
        // 900x900: scale = 900/1280 = 0.703125; css 900x506.25; bars of 196.875 top and bottom.
        CanvasLayout layout = CanvasLayout.Fit(1280, 720, 900, 900);

        Assert.Equal(0.703125, layout.Scale, Tolerance);
        Assert.Equal(900, layout.CssWidth, Tolerance);
        Assert.Equal(506.25, layout.CssHeight, Tolerance);
        Assert.Equal(0, layout.OffsetX, Tolerance);
        Assert.Equal(196.875, layout.OffsetY, Tolerance);

        // Backing/CSS: 1280/900 == 720/506.25.
        Assert.Equal(1280.0 / 900.0, layout.MouseScaleX, Tolerance);
        Assert.Equal(layout.MouseScaleX, layout.MouseScaleY, Tolerance);
    }

    [Fact]
    public void Fit_narrow_viewport_letterboxes_horizontally()
    {
        // 700x900: scale = 700/1280; the canvas is 700x393.75 with no horizontal bars.
        CanvasLayout layout = CanvasLayout.Fit(1280, 720, 700, 900);

        Assert.Equal(700, layout.CssWidth, Tolerance);
        Assert.Equal(393.75, layout.CssHeight, Tolerance);
        Assert.Equal(0, layout.OffsetX, Tolerance);
        Assert.Equal(253.125, layout.OffsetY, Tolerance);
        Assert.Equal(1280.0 / 700.0, layout.MouseScaleX, Tolerance);
    }

    [Fact]
    public void Fit_upscales_beyond_the_logical_size()
    {
        CanvasLayout layout = CanvasLayout.Fit(1280, 720, 2560, 1440);

        Assert.Equal(2, layout.Scale, Tolerance);
        Assert.Equal(2560, layout.CssWidth, Tolerance);
        Assert.Equal(1440, layout.CssHeight, Tolerance);
        Assert.Equal(0.5, layout.MouseScaleX, Tolerance);
        Assert.Equal(0.5, layout.MouseScaleY, Tolerance);
    }

    [Fact]
    public void Fit_integer_scaling_rounds_down_to_whole_multiples()
    {
        // 700/320 = 2.1875 -> 2; 640x360 centered in 700x900.
        CanvasLayout layout = CanvasLayout.Fit(320, 180, 700, 900, integerScaling: true);

        Assert.Equal(2, layout.Scale, Tolerance);
        Assert.Equal(640, layout.CssWidth, Tolerance);
        Assert.Equal(360, layout.CssHeight, Tolerance);
        Assert.Equal(30, layout.OffsetX, Tolerance);
        Assert.Equal(270, layout.OffsetY, Tolerance);
        Assert.Equal(0.5, layout.MouseScaleX, Tolerance);
    }

    [Fact]
    public void Fit_integer_scaling_keeps_fractional_scale_below_one()
    {
        // Rounding 0.703 down to zero would hide the canvas; keep the fractional scale.
        CanvasLayout layout = CanvasLayout.Fit(1280, 720, 900, 900, integerScaling: true);

        Assert.Equal(0.703125, layout.Scale, Tolerance);
    }

    [Theory]
    [InlineData(0, 900)]
    [InlineData(900, 0)]
    [InlineData(-1, -1)]
    public void Fit_unmeasured_viewport_returns_empty_layout(double viewportWidth, double viewportHeight)
    {
        CanvasLayout layout = CanvasLayout.Fit(1280, 720, viewportWidth, viewportHeight);

        Assert.Equal(0, layout.Scale, Tolerance);
        Assert.Equal(1, layout.MouseScaleX, Tolerance);
        Assert.Equal(1, layout.MouseScaleY, Tolerance);
        Assert.Equal(0, layout.OffsetX, Tolerance);
        Assert.Equal(0, layout.OffsetY, Tolerance);
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(-1280, 720)]
    public void Fit_rejects_non_positive_logical_size(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasLayout.Fit(width, height, 800, 600));
    }

    [Fact]
    public void Fill_tracks_the_viewport_one_to_one()
    {
        CanvasLayout layout = CanvasLayout.Fill(800, 600);

        Assert.Equal(1, layout.Scale, Tolerance);
        Assert.Equal(800, layout.CssWidth, Tolerance);
        Assert.Equal(600, layout.CssHeight, Tolerance);
        Assert.Equal(0, layout.OffsetX, Tolerance);
        Assert.Equal(0, layout.OffsetY, Tolerance);
        Assert.Equal(1, layout.MouseScaleX, Tolerance);
        Assert.Equal(1, layout.MouseScaleY, Tolerance);
    }

    [Fact]
    public void Fill_unmeasured_viewport_returns_empty_layout()
    {
        CanvasLayout layout = CanvasLayout.Fill(0, 0);

        Assert.Equal(0, layout.Scale, Tolerance);
        Assert.Equal(0, layout.CssWidth, Tolerance);
        Assert.Equal(0, layout.CssHeight, Tolerance);
    }
}

using Xunit;

namespace Rayforge.Tests;

public class GameOptionsTests
{
    [Fact]
    public void Defaults_match_the_documented_contract()
    {
        var options = new GameOptions();

        Assert.Equal(1280, options.Width);
        Assert.Equal(720, options.Height);
        Assert.Equal(CanvasScaleMode.Fit, options.ScaleMode);
        Assert.Equal(AssetMode.Bundled, options.AssetMode);
        Assert.True(options.EnableAudio);
        Assert.True(options.RequireUserGesture);
        Assert.Equal(0, options.TargetFps);
    }
}

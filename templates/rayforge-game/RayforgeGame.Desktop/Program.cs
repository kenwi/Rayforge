using Rayforge;
using Rayforge.Desktop;
using RayforgeGame;

// The desktop host is only configuration. The same ExampleGame class runs in the browser.
DesktopRunner.Run<ExampleGame>(new GameOptions
{
    Title = "Rayforge Game",
    Width = 1280,
    Height = 720,
    ScaleMode = CanvasScaleMode.Fit,
    AssetMode = AssetMode.Bundled,
    EnableAudio = false,
});

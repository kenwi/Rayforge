using HelloWorld;
using Rayforge;
using Rayforge.Desktop;

DesktopRunner.Run<HelloWorldGame>(new GameOptions
{
    Title = "Hello, Rayforge!",
    Width = 1280,
    Height = 720,
    ScaleMode = CanvasScaleMode.Fit,
    AssetMode = AssetMode.Bundled,

    // M1 has no sounds; M2 turns this on together with the Hello World beep.
    EnableAudio = false,
});

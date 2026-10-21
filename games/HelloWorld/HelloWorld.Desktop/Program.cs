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

    // M2 demo: the beep is the audio-unlock proof on the web.
    EnableAudio = true,
});

using DoomFire;
using Rayforge;
using Rayforge.Desktop;
using Raylib_cs;

DesktopRunner.Run<DoomFireGame>(new GameOptions
{
    Title = "Doom Fire",
    Width = 960,
    Height = 540,
    ScaleMode = CanvasScaleMode.Fill,
    AssetMode = AssetMode.Bundled,
    EnableAudio = false,
    ConfigFlags = ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint,
});

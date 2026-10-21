using Raylib_cs;
using Rayforge;
using DoomFire.Fire;

namespace DoomFire;

/// <summary>
/// Doom fire effect ported from DoomFire-Raylib. One <see cref="FireSlice"/>, no window or loop.
/// Runs unchanged on Blazor WebAssembly and on the desktop.
/// </summary>
public sealed class DoomFireGame : GameBase
{
    private readonly FireSlice _fire = new();

    /// <inheritdoc />
    protected override void OnInitialize()
    {
        _fire.Load(Context.IsBrowser);
        Context.Log.Info(
            $"DoomFireGame ready (browser={Context.IsBrowser}, " +
            $"shaders={(Context.IsBrowser ? "es100" : "gl330")})");
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Context.Runtime.RequestQuit();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F))
        {
            Context.Runtime.ToggleFullscreen();
        }

        _fire.Update(deltaTime);
    }

    /// <inheritdoc />
    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        _fire.Draw();
        Raylib.EndDrawing();
    }

    /// <inheritdoc />
    public override void Dispose() => _fire.Dispose();
}

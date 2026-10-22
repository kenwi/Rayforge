using Raylib_cs;
using Rayforge;

namespace RayforgeGame;

/// <summary>
/// The portable game. Everything in this project must work on both hosts; it has no Blazor, no JS
/// interop and no window or audio lifecycle. Rename this class and the namespace as you like.
/// </summary>
public sealed class ExampleGame : GameBase
{
    private string _message = "Hello from Rayforge";
    private float _time;

    /// <inheritdoc />
    protected override void OnInitialize()
    {
        // Assets are relative, lowercase and live in the Assets/ folder. Both hosts copy the
        // folder into place before the game starts, so this path is identical on web and desktop.
        string message = Raylib.LoadFileText("assets/motd.txt");
        if (!string.IsNullOrWhiteSpace(message))
        {
            _message = message.Trim();
        }

        Context.Log.Info($"ExampleGame ready (browser={Context.IsBrowser})");
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Context.Runtime.RequestQuit();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F))
        {
            Context.Runtime.ToggleFullscreen();
        }
    }

    /// <inheritdoc />
    public override void Draw()
    {
        int width = Raylib.GetScreenWidth();
        int height = Raylib.GetScreenHeight();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(10, 12, 20, 255));

        Raylib.DrawText(_message, 64, 64, 40, new Color(238, 239, 246, 255));
        Raylib.DrawText("F: fullscreen   Esc: quit", 64, 120, 20, new Color(160, 163, 184, 255));
        Raylib.DrawText($"{width} x {height}", 64, 152, 20, new Color(110, 114, 134, 255));

        float pulse = 72f + 8f * MathF.Sin(_time * 2f);
        Raylib.DrawCircle(width / 2, height / 2, pulse, new Color(120, 200, 255, 255));

        Raylib.EndDrawing();
    }
}

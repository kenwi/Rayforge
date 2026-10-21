using Raylib_cs;
using Rayforge;

namespace HelloWorld;

/// <summary>
/// M1 placeholder game. It proves the desktop host, the <see cref="GameContext"/> and raylib rendering;
/// the actual Hello World scenes arrive in M2.
/// </summary>
public sealed class HelloWorldGame : GameBase
{
    private static readonly Color Background = new(10, 10, 16, 255);
    private static readonly Color TitleColor = new(233, 233, 242, 255);
    private static readonly Color BodyColor = new(139, 139, 164, 255);
    private static readonly Color AccentColor = new(159, 176, 255, 255);

    private float _elapsed;

    /// <inheritdoc />
    protected override void OnInitialize()
    {
        Context.Log.Info(
            $"{nameof(HelloWorldGame)} initialized: {Context.Width}x{Context.Height}, " +
            $"browser={Context.IsBrowser}, audio={Context.AudioAvailable}");
        Context.Diagnostics.Status = "running";
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        _elapsed += deltaTime;

        // Escape is a game decision: the host installs no exit key.
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Context.Runtime.RequestQuit();
        }
    }

    /// <inheritdoc />
    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Background);

        Raylib.DrawText("Hello, Rayforge!", 48, 48, 48, TitleColor);
        Raylib.DrawText("M1 skeleton: the loop runs, the rules arrive in M2.", 48, 112, 20, BodyColor);
        Raylib.DrawText(
            $"desktop - {Raylib.GetFPS()} fps - {_elapsed:0.0}s",
            48,
            148,
            20,
            AccentColor);

        Raylib.EndDrawing();
    }
}

namespace Rayforge;

/// <summary>
/// A game the host can run. The host owns the window, the audio device and the frame loop; the game owns
/// <see cref="Update"/> and <see cref="Draw"/> and talks to raylib directly.
/// </summary>
/// <remarks>
/// Ported raylib code keeps its drawing calls. What moves out of the game is the window lifecycle, the
/// blocking loop, cursor capture, fullscreen and file access - those are <see cref="GameContext"/> services.
/// </remarks>
public interface IGame : IDisposable
{
    /// <summary>Called once after the window and the audio device (if enabled) are ready.</summary>
    void Initialize(GameContext context);

    /// <summary>Called once per frame. <paramref name="deltaTime"/> is clamped by the host.</summary>
    void Update(float deltaTime);

    /// <summary>Called once per frame, after <see cref="Update"/>. The game owns BeginDrawing/EndDrawing.</summary>
    void Draw();

    /// <summary>Called when the canvas or window size changes. Logical size may stay fixed in Fit mode.</summary>
    void OnResize(int width, int height);
}

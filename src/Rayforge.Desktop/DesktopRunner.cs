using Raylib_cs;

namespace Rayforge.Desktop;

/// <summary>
/// Runs a game in a native raylib window. Mirrors the browser frame loop: the host owns the window,
/// the audio device and the loop; the game owns Update/Draw and all rendering calls.
/// </summary>
public static class DesktopRunner
{
    /// <summary>Upper bound for a frame delta, matching the browser host (background tab, breakpoint).</summary>
    private const float MaxDeltaTime = 0.1f;

    /// <summary>Creates a window, runs <typeparamref name="T"/> until it quits, then tears everything down.</summary>
    /// <typeparam name="T">The game type. Created once, after the window exists.</typeparam>
    /// <param name="options">Window size, title and host flags.</param>
    /// <exception cref="InvalidOperationException">raylib could not create a window (no display, driver failure).</exception>
    public static void Run<T>(GameOptions options)
        where T : IGame, new()
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Width <= 0 || options.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Width and Height must be positive.");
        }

        IGameLog log = ConsoleGameLog.Instance;

        Raylib.SetConfigFlags(options.ConfigFlags);
        Raylib.InitWindow(options.Width, options.Height, options.Title);

        if (!Raylib.IsWindowReady())
        {
            throw new InvalidOperationException(
                $"raylib could not create a {options.Width}x{options.Height} window. " +
                "Is a display available (DISPLAY/WAYLAND_DISPLAY)?");
        }

        bool audioReady = false;

        try
        {
            // Escape belongs to the game: a port may use it for a menu or to release the mouse.
            Raylib.SetExitKey(KeyboardKey.Null);

            if (options.TargetFps > 0)
            {
                Raylib.SetTargetFPS(options.TargetFps);
            }

            audioReady = options.EnableAudio && TryInitAudio(log);

            var host = new DesktopRuntimeHost();
            var mouse = new DesktopMouseCapture();
            var diagnostics = new GameDiagnostics
            {
                AssetMode = options.AssetMode,
                BackingWidth = Raylib.GetScreenWidth(),
                BackingHeight = Raylib.GetScreenHeight(),
                CssWidth = Raylib.GetScreenWidth(),
                CssHeight = Raylib.GetScreenHeight(),
                Status = "initializing",
            };

            var context = DesktopContext.Create(options, host, mouse, diagnostics, log, audioReady);
            var game = new T();

            try
            {
                game.Initialize(context);
                diagnostics.Status = "running";
                log.Info($"{typeof(T).Name} running at {diagnostics.BackingWidth}x{diagnostics.BackingHeight} (desktop)");

                RunLoop(game, host, mouse, diagnostics);
            }
            finally
            {
                game.Dispose();
                diagnostics.Status = "stopped";
            }
        }
        finally
        {
            if (audioReady)
            {
                Raylib.CloseAudioDevice();
            }

            Raylib.CloseWindow();
        }
    }

    private static void RunLoop(IGame game, DesktopRuntimeHost host, DesktopMouseCapture mouse, GameDiagnostics diagnostics)
    {
        int width = Raylib.GetScreenWidth();
        int height = Raylib.GetScreenHeight();
        float elapsed = 0f;
        int frames = 0;

        while (!host.QuitRequested && !Raylib.WindowShouldClose())
        {
            float deltaTime = MathF.Min(Raylib.GetFrameTime(), MaxDeltaTime);

            game.Update(deltaTime);
            game.Draw();
            mouse.Accumulate();

            int currentWidth = Raylib.GetScreenWidth();
            int currentHeight = Raylib.GetScreenHeight();
            if (currentWidth != width || currentHeight != height)
            {
                width = currentWidth;
                height = currentHeight;
                diagnostics.BackingWidth = width;
                diagnostics.BackingHeight = height;
                diagnostics.CssWidth = width;
                diagnostics.CssHeight = height;
                game.OnResize(width, height);
            }

            elapsed += deltaTime;
            frames++;
            if (elapsed >= 1f)
            {
                diagnostics.Fps = frames / elapsed;
                diagnostics.FrameTimeMilliseconds = deltaTime * 1000.0;
                elapsed = 0f;
                frames = 0;
            }
        }
    }

    private static bool TryInitAudio(IGameLog log)
    {
        try
        {
            Raylib.InitAudioDevice();
        }
        catch (Exception ex)
        {
            log.Warn($"audio device init threw: {ex.Message}");
            return false;
        }

        if (!Raylib.IsAudioDeviceReady())
        {
            log.Warn("audio device unavailable; continuing without sound");
            return false;
        }

        return true;
    }
}

using Raylib_cs;

namespace DoomFire.Fire;

/// <summary>
/// Doom fire feature: wind, fuel, scatter simulation, and the shaded present.
/// </summary>
public sealed class FireSlice : IDisposable
{
    private FireSimulation? _simulation;
    private FirePresenter? _presenter;
    private float _wind;
    private bool _windInitialized;
    private bool _disposed;

    public void Load(bool isBrowser)
    {
        _simulation = new FireSimulation();
        _presenter = new FirePresenter(isBrowser);
        var (gridWidth, gridHeight) = GridSize(Raylib.GetRenderWidth(), Raylib.GetRenderHeight());
        _simulation.EnsureSize(gridWidth, gridHeight);
    }

    public void Update(float deltaTime)
    {
        if (_simulation is null)
        {
            return;
        }

        var (gridWidth, gridHeight) = GridSize(Raylib.GetRenderWidth(), Raylib.GetRenderHeight());
        _simulation.EnsureSize(gridWidth, gridHeight);

        float target = ReadWindTarget();
        if (!_windInitialized)
        {
            _wind = target;
            _windInitialized = true;
        }
        else
        {
            float blend = 1f - MathF.Exp(-deltaTime * 10f);
            _wind = float.Lerp(_wind, target, blend);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            _simulation.Emitting = !_simulation.Emitting;
        }

        if (_presenter is not null && Raylib.IsKeyPressed(KeyboardKey.One))
        {
            _presenter.Effects = !_presenter.Effects;
        }

        if (_presenter is not null && Raylib.IsKeyPressed(KeyboardKey.Two))
        {
            _presenter.CyclePalette();
        }

        _simulation.Step(_wind);
    }

    public void Draw()
    {
        if (_simulation is null || _presenter is null)
        {
            return;
        }

        int width = Raylib.GetRenderWidth();
        int height = Raylib.GetRenderHeight();
        if (width < 2 || height < 2)
        {
            return;
        }

        _presenter.Draw(_simulation.Heat, width, height);
        DrawHud();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _presenter?.Dispose();
        _simulation?.Dispose();
    }

    // The article's cool burns out in about 70 rows. A grid a bit taller than
    // that leaves black above the tongues, and the present pass scales the
    // cells up so the shape matches the original framebuffer.
    private static (int Width, int Height) GridSize(int screenWidth, int screenHeight)
    {
        const int rows = 140;
        int cell = Math.Max(1, screenHeight / rows);
        return (Math.Max(2, screenWidth / cell), Math.Max(2, screenHeight / cell));
    }

    private static float ReadWindTarget()
    {
        if (Raylib.IsKeyDown(KeyboardKey.Left) || Raylib.IsKeyDown(KeyboardKey.A))
        {
            return -1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.Right) || Raylib.IsKeyDown(KeyboardKey.D))
        {
            return 1f;
        }

        return 0f;
    }

    private void DrawHud()
    {
        string fuel = _simulation?.Emitting == true ? "on" : "off";
        string fx = _presenter?.Effects == true ? "on" : "off";
        string palette = _presenter?.PaletteName ?? "doom";
        Raylib.DrawRectangle(8, 8, 920, 46, new Color(0, 0, 0, 150));
        Raylib.DrawText("Doom fire", 16, 14, 20, new Color(255, 220, 180, 255));
        Raylib.DrawText(
            $"wind {_wind,5:0.00}    A/D or arrows    space fuel {fuel}    1 fx {fx}    2 {palette}    F fullscreen    Esc quits",
            16,
            36,
            16,
            new Color(220, 180, 140, 255));
    }
}

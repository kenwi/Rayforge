using System.Numerics;
using Raylib_cs;
using Rayforge;

namespace HelloWorld;

/// <summary>
/// The M2 demo: a rotating box, text, a texture, a custom font, a beep, click rings and a mouse marker.
/// Runs unchanged on Blazor WebAssembly and on the desktop. No <c>#if</c>, no host types.
/// </summary>
public sealed class HelloWorldGame : GameBase
{
    private const float RingLifetime = 0.6f;
    private const int MaxRings = 32;
    private const float DefaultFontSize = 48f;

    private static readonly Color Background = new(12, 14, 22, 255);
    private static readonly Color Surface = new(24, 27, 40, 255);
    private static readonly Color GridColor = new(34, 39, 56, 255);
    private static readonly Color TitleColor = new(238, 239, 246, 255);
    private static readonly Color BodyColor = new(160, 163, 184, 255);
    private static readonly Color DimColor = new(110, 114, 134, 255);
    private static readonly Color AccentColor = new(120, 255, 160, 255);
    private static readonly Color BoxColor = new(232, 108, 42, 255);
    private static readonly Color BoxEdgeColor = new(255, 196, 140, 255);

    private readonly List<Ring> _rings = [];

    private Texture2D _logo;
    private Font _font;
    private Sound _blip;
    private bool _ownsFont;
    private bool _audio;
    private bool _rotate = true;
    private bool _showDiagnostics = true;
    private float _angle;

    /// <inheritdoc />
    protected override void OnInitialize()
    {
        _audio = Context.AudioAvailable;

        _logo = Raylib.LoadTexture("assets/logo.png");
        if (!Raylib.IsTextureValid(_logo))
        {
            Context.Log.Warn("assets/logo.png is missing or invalid; drawing a placeholder");
        }

        _font = Raylib.LoadFontEx("assets/ui.ttf", (int)DefaultFontSize, null, 0);
        if (Raylib.IsFontValid(_font))
        {
            _ownsFont = true;
            Raylib.SetTextureFilter(_font.Texture, TextureFilter.Bilinear);
        }
        else
        {
            Context.Log.Warn("assets/ui.ttf is missing or invalid; using the raylib default font");
            _font = Raylib.GetFontDefault();
            _ownsFont = false;
        }

        if (_audio)
        {
            _blip = Raylib.LoadSound("assets/blip.ogg");
            if (_blip.FrameCount <= 0)
            {
                Context.Log.Warn("assets/blip.ogg is missing or invalid; clicks are silent");
                _audio = false;
            }
            else
            {
                Raylib.SetSoundVolume(_blip, 0.4f);
            }
        }

        Context.Log.Info(
            $"HelloWorldGame ready (browser={Context.IsBrowser}, audio={_audio}, " +
            $"font={(RayforgeFontName())}, logo={_logo.Width}x{_logo.Height})");
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            _rotate = !_rotate;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            _angle = 0f;
            _rings.Clear();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F))
        {
            Context.Runtime.ToggleFullscreen();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.D) || Raylib.IsKeyPressed(KeyboardKey.F3))
        {
            _showDiagnostics = !_showDiagnostics;
        }

        // Deliberate crash flag for the error-path acceptance test: the host must stop the loop,
        // unload the game and show the overlay instead of leaving a frozen black canvas.
        if (Raylib.IsKeyPressed(KeyboardKey.X))
        {
            throw new InvalidOperationException("Intentional crash: X was pressed.");
        }

        if (_rotate)
        {
            _angle += deltaTime * 45f;
            if (_angle >= 360f)
            {
                _angle -= 360f;
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (_audio)
            {
                Raylib.PlaySound(_blip);
            }

            if (_rings.Count >= MaxRings)
            {
                _rings.RemoveAt(0);
            }

            _rings.Add(new Ring(Raylib.GetMousePosition(), 0f));
        }

        for (int i = 0; i < _rings.Count; i++)
        {
            Ring ring = _rings[i];
            _rings[i] = ring with { Age = ring.Age + deltaTime };
        }

        _rings.RemoveAll(ring => ring.Age > RingLifetime);
    }

    /// <inheritdoc />
    public override void Draw()
    {
        int width = Raylib.GetScreenWidth();
        int height = Raylib.GetScreenHeight();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Background);

        DrawScene();
        DrawBorder(width, height);

        foreach (Ring ring in _rings)
        {
            float t = Math.Clamp(ring.Age / RingLifetime, 0f, 1f);
            Color color = Raylib.Fade(AccentColor, 1f - t);
            Raylib.DrawCircleLinesV(ring.Position, 8f + 44f * t, color);
            Raylib.DrawCircleLinesV(ring.Position, 2f + 44f * t, Raylib.Fade(AccentColor, 0.4f * (1f - t)));
        }

        DrawHud(width, height);
        DrawMouseMarker();

        Raylib.EndDrawing();
    }

    private void DrawScene()
    {
        // Fixed camera, rotating model: exactly what the spec describes. The grid stays put, so motion
        // reads as the cube turning rather than the whole world orbiting.
        var camera = new Camera3D
        {
            Position = new Vector3(4.8f, 4.0f, 4.8f),
            Target = new Vector3(0f, 1f, 0f),
            Up = Vector3.UnitY,
            FovY = 45f,
            Projection = CameraProjection.Perspective,
        };

        Raylib.BeginMode3D(camera);

        // The grid sits at y=0, below the cube. Drawing it through the cube's centre looks like
        // wireframe bleeding through the faces.
        Raylib.DrawGrid(12, 1f);

        var size = new Vector3(1.8f, 1.8f, 1.8f);

        Rlgl.PushMatrix();
        Rlgl.Translatef(0f, 1f, 0f);
        Rlgl.Rotatef(_angle, 0f, 1f, 0f);
        Rlgl.Rotatef(_angle * 0.2f, 1f, 0f, 0f);

        Raylib.DrawCubeV(Vector3.Zero, size, BoxColor);
        Raylib.DrawCubeWiresV(Vector3.Zero, size, BoxEdgeColor);

        // An asymmetric marker on one corner: a symmetric box alone would look static.
        Raylib.DrawSphere(new Vector3(size.X * 0.5f, size.Y * 0.5f, size.Z * 0.5f), 0.16f, AccentColor);

        Rlgl.PopMatrix();

        Raylib.EndMode3D();
    }

    private void DrawBorder(int width, int height)
    {
        Raylib.DrawRectangleLines(8, 8, width - 16, height - 16, GridColor);
    }

    private void DrawHud(int width, int height)
    {
        DrawText("Hello, Rayforge!", 24f, 18f, 32f, TitleColor);

        string header = $"{Context.Diagnostics.Fps,3:0} FPS   {width}x{height}   {(Context.IsBrowser ? "web" : "desktop")}";
        float headerWidth = Raylib.MeasureTextEx(_font, header, 18f, 1f).X;
        DrawText(header, width - 24f - headerWidth, 26f, 18f, DimColor);

        if (_showDiagnostics)
        {
            DrawText(
                $"host {Context.Diagnostics.Fps:0.0} fps - {Context.Diagnostics.FrameTimeMilliseconds:0.0} ms",
                24f,
                58f,
                16f,
                BodyColor);
            DrawText(
                $"backing {Context.Diagnostics.BackingWidth}x{Context.Diagnostics.BackingHeight} - " +
                $"css {Context.Diagnostics.CssWidth}x{Context.Diagnostics.CssHeight} - " +
                $"mouse scale {Context.Diagnostics.MouseScaleX:0.###}",
                24f,
                80f,
                16f,
                BodyColor);
            DrawText(
                $"assets {Context.Diagnostics.AssetMode} - audio {(_audio ? "on" : "off")} - " +
                $"fullscreen {(Context.Runtime.IsFullscreen ? "on" : "off")}",
                24f,
                102f,
                16f,
                BodyColor);
        }

        if (Raylib.IsTextureValid(_logo))
        {
            Raylib.DrawTexture(_logo, 24, height - 88, Color.White);
        }
        else
        {
            Raylib.DrawRectangle(24, height - 88, 64, 64, Surface);
            Raylib.DrawRectangleLines(24, height - 88, 64, 64, GridColor);
        }

        DrawText(
            Context.IsBrowser ? "raylib 6.0 - Blazor WebAssembly" : "raylib 6.0 - desktop",
            100f,
            height - 80f,
            18f,
            BodyColor);

        DrawText(
            "SPACE pause - R reset - F fullscreen - D diagnostics - click = beep + ring",
            100f,
            height - 54f,
            16f,
            DimColor);
    }

    private void DrawMouseMarker()
    {
        Vector2 mouse = Raylib.GetMousePosition();
        Raylib.DrawCircleV(mouse, 4f, AccentColor);
        Raylib.DrawCircleLinesV(mouse, 12f, AccentColor);
    }

    private void DrawText(string text, float x, float y, float size, Color color)
        => Raylib.DrawTextEx(_font, text, new Vector2(x, y), size, 1f, color);

    private string RayforgeFontName() => _ownsFont ? "Rayforge Sans" : "default";

    /// <inheritdoc />
    public override void Dispose()
    {
        if (_ownsFont && Raylib.IsFontValid(_font))
        {
            Raylib.UnloadFont(_font);
        }

        if (Raylib.IsTextureValid(_logo))
        {
            Raylib.UnloadTexture(_logo);
        }

        if (_audio && _blip.FrameCount > 0)
        {
            Raylib.UnloadSound(_blip);
        }
    }

    private readonly record struct Ring(Vector2 Position, float Age);
}

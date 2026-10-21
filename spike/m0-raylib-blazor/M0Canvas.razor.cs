using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Raylib_cs;
using Color = Raylib_cs.Color;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace M0;

/// <summary>
/// M0 probe. Deliberately a single component: the point is to prove the native link,
/// the canvas binding, the rAF -> [JSExport] loop, bundled assets, audio and mouse
/// mapping in a *fresh* Blazor WASM project. Nothing here is production code.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class M0Canvas : IDisposable
{
    private const int LogicalWidth = 1280;
    private const int LogicalHeight = 720;

    [Inject]
    public required NavigationManager Navigation { get; set; }

    public string CanvasId { get; } = "m0-canvas";

    private bool _started;
    private string? _error;

    private Texture2D _logo;
    private Sound _blip;
    private bool _audioReady;
    private int _clicks;

    private bool _rotate = true;
    private float _angle;
    private readonly List<(Vector2 Pos, float Age)> _rings = new();

    private float _cssW = LogicalWidth;
    private float _cssH = LogicalHeight;
    private float _scaleX = 1f;
    private float _scaleY = 1f;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _started) return;
        _started = true;

        try
        {
            // 1. Bring in the JS side and bind the canvas to the Emscripten module.
            //    Relative module URLs resolve against _framework/, not the document base,
            //    so hand JSHost an absolute URL built from the app base.
            var moduleUrl = new Uri(new Uri(Navigation.BaseUri), "js/m0.js").AbsoluteUri;
            Console.WriteLine($"[m0] importing {moduleUrl}");
            await JSHost.ImportAsync("m0", moduleUrl);
            M0Interop.Init(CanvasId);
            Console.WriteLine("[m0] canvas bound");

            // 2. Assets must be in the VFS before raylib asks for them.
            await M0Interop.Preload(new[] { "assets/logo.png", "assets/blip.wav" });

            // 3. Window + audio.
            Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
            Raylib.InitWindow(LogicalWidth, LogicalHeight, "M0 - Rayforge spike");
            Console.WriteLine($"[m0] window ready={Raylib.IsWindowReady()}");
            Raylib.InitAudioDevice();
            _audioReady = Raylib.IsAudioDeviceReady();
            Console.WriteLine($"[m0] audio ready={_audioReady}");

            // 3. Bundled assets: present in the VFS before the first frame.
            Console.WriteLine($"[m0] File.Exists assets/logo.png = {File.Exists("assets/logo.png")}");
            Console.WriteLine($"[m0] File.Exists assets/blip.wav = {File.Exists("assets/blip.wav")}");
            _logo = Raylib.LoadTexture("assets/logo.png");
            Console.WriteLine($"[m0] logo {_logo.Width}x{_logo.Height} id={_logo.Id}");
            _blip = Raylib.LoadSound("assets/blip.wav");
            Console.WriteLine($"[m0] blip frames={_blip.FrameCount}");

            // 4. Start the rAF loop.
            M0Interop.Start(this, typeof(M0Canvas).Assembly.GetName().Name!);
            Console.WriteLine("[m0] loop started");
        }
        catch (Exception ex)
        {
            _error = ex.ToString();
            Console.WriteLine("[m0] BOOT FAILED " + _error);
            StateHasChanged();
        }
    }

    // ------------------------------------------------------------------ frame

    [JSExport]
    public static bool Frame([JSMarshalAs<JSType.Any>] object handle, float dt)
    {
        if (handle is not M0Canvas c) return false;

        try
        {
            c.Tick(dt);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[m0] FRAME FAILED " + ex);
            return false;
        }
    }

    [JSExport]
    public static void OnCanvasResize([JSMarshalAs<JSType.Any>] object handle,
                                      int backingW, int backingH, float cssW, float cssH)
    {
        if (handle is not M0Canvas c) return;
        c._cssW = cssW;
        c._cssH = cssH;

        // Raylib reports mouse position in CSS pixels; map to the backing buffer.
        c._scaleX = backingW / cssW;
        c._scaleY = backingH / cssH;
        Raylib.SetMouseScale(c._scaleX, c._scaleY);

        Console.WriteLine($"[m0] resize backing={backingW}x{backingH} css={cssW}x{cssH} scale={c._scaleX:0.###},{c._scaleY:0.###}");
    }

    private void Tick(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Space)) _rotate = !_rotate;
        if (Raylib.IsKeyPressed(KeyboardKey.R)) { _angle = 0; _rings.Clear(); }
        if (Raylib.IsKeyPressed(KeyboardKey.F))
        {
            _ = M0Interop.ToggleFullscreen(CanvasId);
        }

        if (_rotate) _angle += dt * 45f;

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _clicks++;
            if (_audioReady) Raylib.PlaySound(_blip);
            _rings.Add((Raylib.GetMousePosition(), 0f));
            if (_rings.Count > 32) _rings.RemoveAt(0);
        }

        for (int i = 0; i < _rings.Count; i++)
        {
            var r = _rings[i];
            _rings[i] = (r.Pos, r.Age + dt);
        }
        _rings.RemoveAll(r => r.Age > 0.6f);

        Draw();
    }

    private void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(14, 16, 24, 255));

        DrawReferences();
        DrawCubeDemo();
        DrawHud();
        DrawRings();
        DrawMouseMarker();

        Raylib.EndDrawing();
    }

    private void DrawReferences()
    {
        var dim = new Color(38, 44, 62, 255);
        Raylib.DrawRectangleLines(8, 8, LogicalWidth - 16, LogicalHeight - 16, dim);

        // Quarter lines: a known grid for eyeballing the mouse mapping.
        foreach (int x in new[] { 320, 640, 960 })
            Raylib.DrawLine(x, 0, x, LogicalHeight, dim);
        foreach (int y in new[] { 180, 360, 540 })
            Raylib.DrawLine(0, y, LogicalWidth, y, dim);
    }

    private void DrawCubeDemo()
    {
        // Raylib-cs 7.0.2 does not expose rlgl's PushMatrix/Rotatef, so the "rotation"
        // is done by orbiting the camera around a deliberately non-cubic box.
        float rad = _angle * MathF.PI / 180f;
        var camera = new Camera3D
        {
            Position = new Vector3(MathF.Sin(rad) * 5.2f, 3.0f, MathF.Cos(rad) * 5.2f),
            Target = Vector3.Zero,
            Up = Vector3.UnitY,
            FovY = 45f,
            Projection = CameraProjection.Perspective,
        };

        var size = new Vector3(1.6f, 2.2f, 2.6f);

        Raylib.BeginMode3D(camera);
        Raylib.DrawCubeV(Vector3.Zero, size, new Color(232, 108, 42, 255));
        Raylib.DrawCubeWiresV(Vector3.Zero, size, new Color(255, 214, 170, 255));
        Raylib.DrawSphere(new Vector3(0, size.Y / 2, size.Z / 2), 0.22f, new Color(120, 255, 160, 255));
        Raylib.EndMode3D();
    }

    private void DrawHud()
    {
        Raylib.DrawText("M0: Raylib-cs 7.0.2 + raylib.a in Blazor WebAssembly", 24, 24, 28,
                        new Color(240, 240, 245, 255));

        var mouse = Raylib.GetMousePosition();
        Raylib.DrawText(
            $"FPS {Raylib.GetFPS()}   {Raylib.GetScreenWidth()}x{Raylib.GetScreenHeight()}   " +
            $"mouse {mouse.X:0},{mouse.Y:0}   scale {_scaleX:0.###},{_scaleY:0.###}   " +
            $"css {_cssW:0}x{_cssH:0}   clicks {_clicks}   audio {_audioReady}",
            24, 60, 18, new Color(150, 200, 255, 255));

        Raylib.DrawText("SPACE pause rotation   R reset   F fullscreen   click = beep + ring",
                        24, 86, 18, new Color(130, 140, 165, 255));

        if (_logo.Id != 0)
        {
            Raylib.DrawTexture(_logo, 24, LogicalHeight - 88, Color.White);
            Raylib.DrawText("assets/logo.png", 100, LogicalHeight - 64, 18,
                            new Color(130, 140, 165, 255));
        }
        else
        {
            Raylib.DrawText("logo.png MISSING", 24, LogicalHeight - 64, 18, Color.Red);
        }
    }

    private void DrawRings()
    {
        foreach (var (pos, age) in _rings)
        {
            float t = age / 0.6f;
            var col = Raylib.Fade(new Color(255, 170, 90, 255), 1f - t);
            Raylib.DrawCircleLinesV(pos, 8f + 40f * t, col);
        }
    }

    private void DrawMouseMarker()
    {
        var m = Raylib.GetMousePosition();
        Raylib.DrawCircleV(m, 6f, new Color(120, 255, 160, 255));
        Raylib.DrawCircleLinesV(m, 12f, new Color(120, 255, 160, 255));
    }

    private void Reload() => M0Interop.Reload();

    public void Dispose()
    {
        M0Interop.Stop();
        if (Raylib.IsWindowReady())
        {
            if (_logo.Id != 0) Raylib.UnloadTexture(_logo);
            if (_blip.FrameCount > 0) Raylib.UnloadSound(_blip);
            if (_audioReady) Raylib.CloseAudioDevice();
            Raylib.CloseWindow();
        }
    }
}

[SupportedOSPlatform("browser")]
internal static partial class M0Interop
{
    [JSImport("init", "m0")]
    internal static partial void Init(string canvasId);

    [JSImport("start", "m0")]
    internal static partial void Start([JSMarshalAs<JSType.Any>] object handle, string assemblyName);

    [JSImport("stop", "m0")]
    internal static partial void Stop();

    [JSImport("preload", "m0")]
    internal static partial Task Preload(string[] paths);

    [JSImport("toggleFullscreen", "m0")]
    internal static partial Task ToggleFullscreen(string canvasId);

    [JSImport("reload", "m0")]
    internal static partial void Reload();
}

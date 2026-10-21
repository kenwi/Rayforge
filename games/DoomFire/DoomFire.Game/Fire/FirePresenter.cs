using System.Numerics;
using Raylib_cs;

namespace DoomFire.Fire;

/// <summary>
/// Palette present, edge smoothing, and a two-width glow of the hot core.
/// </summary>
internal sealed class FirePresenter : IDisposable
{
    private readonly Shader _present;
    private readonly Shader _bright;
    private readonly Shader _blur;
    private readonly int _paletteLoc;
    private readonly int _softenedLoc;
    private readonly int _heatSizeLoc;
    private readonly int _texelStepLoc;
    private RenderTexture2D _scene;
    private RenderTexture2D _smoothA;
    private RenderTexture2D _smoothB;
    private RenderTexture2D _brightTarget;
    private RenderTexture2D _ping;
    private RenderTexture2D _pong;
    private int _width;
    private int _height;
    private int _smoothWidth;
    private int _smoothHeight;
    private int _bloomWidth;
    private int _bloomHeight;
    private int _paletteIndex;
    private bool _targetsReady;
    private bool _disposed;

    public bool Effects { get; set; } = true;

    public string PaletteName => FirePalette.All[_paletteIndex].Name;

    public void CyclePalette()
    {
        _paletteIndex = (_paletteIndex + 1) % FirePalette.All.Count;
    }

    public FirePresenter(bool isBrowser)
    {
        _present = ShaderAssets.Load(isBrowser, "fire_present.frag");
        _bright = ShaderAssets.Load(isBrowser, "fire_bright.frag");
        _blur = ShaderAssets.Load(isBrowser, "fire_blur.frag");

        _paletteLoc = Raylib.GetShaderLocation(_present, "palette");
        if (_paletteLoc < 0)
        {
            _paletteLoc = Raylib.GetShaderLocation(_present, "palette[0]");
        }

        if (_paletteLoc < 0)
        {
            throw new InvalidOperationException("Present shader is missing uniform 'palette'.");
        }

        _softenedLoc = Raylib.GetShaderLocation(_present, "softened");
        if (_softenedLoc < 0)
        {
            throw new InvalidOperationException("Present shader is missing uniform 'softened'.");
        }

        _heatSizeLoc = Raylib.GetShaderLocation(_present, "heatSize");
        if (_heatSizeLoc < 0)
        {
            throw new InvalidOperationException("Present shader is missing uniform 'heatSize'.");
        }

        _texelStepLoc = Raylib.GetShaderLocation(_blur, "texelStep");
        if (_texelStepLoc < 0)
        {
            throw new InvalidOperationException("Blur shader is missing uniform 'texelStep'.");
        }
    }

    public void Draw(Texture2D heat, int width, int height)
    {
        Raylib.SetShaderValueV(
            _present,
            _paletteLoc,
            FirePalette.All[_paletteIndex].Colors,
            ShaderUniformDataType.Vec3,
            FirePalette.Count);

        Raylib.SetShaderValue(
            _present,
            _heatSizeLoc,
            new Vector2(heat.Width, heat.Height),
            ShaderUniformDataType.Vec2);

        if (!Effects)
        {
            Raylib.SetShaderValue(_present, _softenedLoc, 0f, ShaderUniformDataType.Float);
            Raylib.BeginShaderMode(_present);
            var source = new Rectangle(0, 0, heat.Width, heat.Height);
            var destination = new Rectangle(0, 0, width, height);
            Raylib.DrawTexturePro(heat, source, destination, Vector2.Zero, 0, Color.White);
            Raylib.EndShaderMode();
            return;
        }

        EnsureTargets(width, height);
        Raylib.SetShaderValue(_present, _softenedLoc, 1f, ShaderUniformDataType.Float);

        Raylib.BeginTextureMode(_scene);
        Raylib.ClearBackground(Color.Black);
        Raylib.BeginShaderMode(_present);
        var heatSource = new Rectangle(0, 0, heat.Width, heat.Height);
        var sceneDest = new Rectangle(0, 0, _width, _height);
        Raylib.DrawTexturePro(heat, heatSource, sceneDest, Vector2.Zero, 0, Color.White);
        Raylib.EndShaderMode();
        Raylib.EndTextureMode();

        Raylib.SetTextureFilter(_scene.Texture, TextureFilter.Bilinear);
        Copy(_scene.Texture, _smoothA);
        Blur(_smoothA.Texture, _smoothB, new Vector2(1.0f / _smoothWidth, 0f));
        Blur(_smoothB.Texture, _smoothA, new Vector2(0f, 1.0f / _smoothHeight));

        Raylib.SetTextureFilter(_smoothA.Texture, TextureFilter.Bilinear);
        Blit(_bright, _smoothA.Texture, _brightTarget);

        BlurSeparable(_brightTarget.Texture, 2.2f);
        DrawToScreen(_smoothA.Texture, Color.White);
        Raylib.BeginBlendMode(BlendMode.Additive);
        DrawToScreen(_pong.Texture, Color.White);
        BlurSeparable(_pong.Texture, 5.5f);
        DrawToScreen(_pong.Texture, new Color(170, 140, 90, 255));
        Raylib.BeginBlendMode(BlendMode.Alpha);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseTargets();
        Raylib.UnloadShader(_present);
        Raylib.UnloadShader(_bright);
        Raylib.UnloadShader(_blur);
    }

    private void BlurSeparable(Texture2D source, float spread)
    {
        Raylib.SetTextureFilter(source, TextureFilter.Point);
        Raylib.SetTextureFilter(_ping.Texture, TextureFilter.Point);
        Blur(source, _ping, new Vector2(spread / _bloomWidth, 0f));
        Blur(_ping.Texture, _pong, new Vector2(0f, spread / _bloomHeight));
    }

    private void Blur(Texture2D source, RenderTexture2D dest, Vector2 texelStep)
    {
        Raylib.SetTextureFilter(source, TextureFilter.Point);
        Raylib.SetShaderValue(_blur, _texelStepLoc, texelStep, ShaderUniformDataType.Vec2);
        Blit(_blur, source, dest);
    }

    private static void Copy(Texture2D source, RenderTexture2D dest)
    {
        Raylib.BeginTextureMode(dest);
        Raylib.ClearBackground(Color.Black);
        var sourceRect = new Rectangle(0, 0, source.Width, -source.Height);
        var destRect = new Rectangle(0, 0, dest.Texture.Width, dest.Texture.Height);
        Raylib.DrawTexturePro(source, sourceRect, destRect, Vector2.Zero, 0, Color.White);
        Raylib.EndTextureMode();
    }

    private static void Blit(Shader shader, Texture2D source, RenderTexture2D dest)
    {
        Raylib.BeginTextureMode(dest);
        Raylib.ClearBackground(Color.Black);
        Raylib.BeginShaderMode(shader);
        var sourceRect = new Rectangle(0, 0, source.Width, -source.Height);
        var destRect = new Rectangle(0, 0, dest.Texture.Width, dest.Texture.Height);
        Raylib.DrawTexturePro(source, sourceRect, destRect, Vector2.Zero, 0, Color.White);
        Raylib.EndShaderMode();
        Raylib.EndTextureMode();
    }

    private void DrawToScreen(Texture2D texture, Color tint)
    {
        Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
        var source = new Rectangle(0, 0, texture.Width, -texture.Height);
        var dest = new Rectangle(0, 0, _width, _height);
        Raylib.DrawTexturePro(texture, source, dest, Vector2.Zero, 0, tint);
    }

    private void EnsureTargets(int width, int height)
    {
        if (_targetsReady && _width == width && _height == height)
        {
            return;
        }

        ReleaseTargets();
        _width = width;
        _height = height;
        _smoothWidth = Math.Max(2, width / 2);
        _smoothHeight = Math.Max(2, height / 2);
        _bloomWidth = Math.Max(2, width / 4);
        _bloomHeight = Math.Max(2, height / 4);
        _scene = Raylib.LoadRenderTexture(width, height);
        _smoothA = Raylib.LoadRenderTexture(_smoothWidth, _smoothHeight);
        _smoothB = Raylib.LoadRenderTexture(_smoothWidth, _smoothHeight);
        _brightTarget = Raylib.LoadRenderTexture(_bloomWidth, _bloomHeight);
        _ping = Raylib.LoadRenderTexture(_bloomWidth, _bloomHeight);
        _pong = Raylib.LoadRenderTexture(_bloomWidth, _bloomHeight);
        Clamp(_scene);
        Clamp(_smoothA);
        Clamp(_smoothB);
        Clamp(_brightTarget);
        Clamp(_ping);
        Clamp(_pong);
        _targetsReady = true;
    }

    private void ReleaseTargets()
    {
        if (!_targetsReady)
        {
            return;
        }

        Raylib.UnloadRenderTexture(_scene);
        Raylib.UnloadRenderTexture(_smoothA);
        Raylib.UnloadRenderTexture(_smoothB);
        Raylib.UnloadRenderTexture(_brightTarget);
        Raylib.UnloadRenderTexture(_ping);
        Raylib.UnloadRenderTexture(_pong);
        _targetsReady = false;
    }

    private static void Clamp(RenderTexture2D target)
    {
        Raylib.SetTextureWrap(target.Texture, TextureWrap.Clamp);
    }
}

using Raylib_cs;

namespace DoomFire.Fire;

/// <summary>
/// In-place scatter from Fabien Sanglard's article. Each cell writes one
/// neighbor above it. Cells that are not written keep their previous heat,
/// which is what pulls the tips into separate flames instead of a flat row.
/// </summary>
internal sealed class FireSimulation : IDisposable
{
    private readonly Random _random = new();
    private byte[] _heat = [];
    private byte[] _pixels = [];
    private Texture2D _texture;
    private int _width;
    private int _height;
    private bool _ready;
    private bool _disposed;

    public Texture2D Heat => _texture;

    public bool Emitting { get; set; } = true;

    public void EnsureSize(int width, int height)
    {
        if (width < 2 || height < 2)
        {
            return;
        }

        if (_ready && width == _width && height == _height)
        {
            return;
        }

        if (_ready)
        {
            Raylib.UnloadTexture(_texture);
        }

        _width = width;
        _height = height;
        _heat = new byte[width * height];
        _pixels = new byte[width * height * 4];
        if (Emitting)
        {
            IgniteBottom();
        }

        Image image = Raylib.GenImageColor(width, height, Color.Black);
        _texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        Raylib.SetTextureFilter(_texture, TextureFilter.Point);
        Raylib.SetTextureWrap(_texture, TextureWrap.Clamp);
        Publish();
        _ready = true;
    }

    public void Step(float wind)
    {
        if (!_ready)
        {
            return;
        }

        int windSteps = (int)Math.Round(Math.Clamp(wind, -1f, 1f) * 3.0);
        int length = _heat.Length;
        for (int src = 0; src < length; src++)
        {
            int rand = (int)Math.Round(_random.NextDouble() * 3.0) & 3;
            // The article's (1 - rand) offsets are +1, 0, -1, -2, so still air
            // drifts left. These four are centered: -1, 0, 0, +1.
            int shift = rand switch
            {
                0 => -1,
                3 => 1,
                _ => 0,
            };
            int index = Math.Max(0, src - _width + shift - windSteps);
            if (index >= length)
            {
                continue;
            }

            int cooled = _heat[src] - (rand & 1);
            _heat[index] = (byte)Math.Max(0, cooled);
        }

        if (Emitting)
        {
            IgniteBottom();
        }
        else
        {
            CoolBottom();
        }

        Publish();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ready)
        {
            Raylib.UnloadTexture(_texture);
            _ready = false;
        }
    }

    private void IgniteBottom()
    {
        int bottom = (_height - 1) * _width;
        for (int x = 0; x < _width; x++)
        {
            _heat[bottom + x] = FirePalette.MaxIndex;
        }
    }

    private void CoolBottom()
    {
        int bottom = (_height - 1) * _width;
        for (int x = 0; x < _width; x++)
        {
            int rand = (int)Math.Round(_random.NextDouble() * 3.0) & 3;
            int cooled = _heat[bottom + x] - (rand & 1);
            _heat[bottom + x] = (byte)Math.Max(0, cooled);
        }
    }

    private void Publish()
    {
        for (int i = 0; i < _heat.Length; i++)
        {
            int pixel = i * 4;
            _pixels[pixel] = _heat[i];
            _pixels[pixel + 1] = 0;
            _pixels[pixel + 2] = 0;
            _pixels[pixel + 3] = 255;
        }

        Raylib.UpdateTexture(_texture, _pixels);
    }
}

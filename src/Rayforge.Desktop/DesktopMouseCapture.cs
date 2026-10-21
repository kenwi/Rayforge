using System.Numerics;
using Raylib_cs;

namespace Rayforge.Desktop;

/// <summary>Cursor capture through raylib's <c>DisableCursor</c>/<c>EnableCursor</c>.</summary>
public sealed class DesktopMouseCapture : IMouseCapture
{
    private Vector2 _pendingDelta;

    /// <inheritdoc />
    public bool IsCaptured { get; private set; }

    /// <inheritdoc />
    public void Request()
    {
        if (IsCaptured)
        {
            return;
        }

        Raylib.DisableCursor();
        _pendingDelta = Vector2.Zero;
        IsCaptured = true;
    }

    /// <inheritdoc />
    public void Release()
    {
        if (!IsCaptured)
        {
            return;
        }

        Raylib.EnableCursor();
        _pendingDelta = Vector2.Zero;
        IsCaptured = false;
    }

    /// <inheritdoc />
    public Vector2 ConsumeDelta()
    {
        Vector2 delta = _pendingDelta;
        _pendingDelta = Vector2.Zero;
        return delta;
    }

    /// <summary>Called once per frame by the runner, after the game had a chance to read input.</summary>
    internal void Accumulate()
    {
        if (IsCaptured)
        {
            _pendingDelta += Raylib.GetMouseDelta();
        }
    }
}

using System.Numerics;

namespace Rayforge;

/// <summary>
/// Cursor capture. On the web this is the Pointer Lock API and <see cref="Request"/> must run inside a
/// user gesture; on desktop it is <c>DisableCursor</c>/<c>EnableCursor</c>.
/// </summary>
public interface IMouseCapture
{
    /// <summary>True while the pointer/cursor is captured.</summary>
    bool IsCaptured { get; }

    /// <summary>Capture the pointer. Web: call from a click/key handler, not from the frame loop.</summary>
    void Request();

    /// <summary>Release the pointer. On the web, Escape does this too (browser behavior).</summary>
    void Release();

    /// <summary>Returns accumulated movement since the previous call and clears it.</summary>
    Vector2 ConsumeDelta();
}

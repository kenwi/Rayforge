using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Rayforge.Web;

/// <summary>
/// Browser pointer lock. <see cref="Request"/> forwards to the Pointer Lock API, so it only works when
/// called while a user gesture is active (the demo does not use it yet; see HELLO-WORLD stretch goals).
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserMouseCapture : IMouseCapture
{
    /// <inheritdoc />
    public bool IsCaptured { get; private set; }

    /// <inheritdoc />
    public void Request() => RayforgeInterop.RequestPointerLock();

    /// <inheritdoc />
    public void Release()
    {
        RayforgeInterop.ReleasePointerLock();
        IsCaptured = false;
    }

    /// <inheritdoc />
    public Vector2 ConsumeDelta()
    {
        if (!IsCaptured)
        {
            return Vector2.Zero;
        }

        using JSObject delta = RayforgeInterop.ConsumePointerDelta();
        return new Vector2(
            (float)delta.GetPropertyAsDouble("x"),
            (float)delta.GetPropertyAsDouble("y"));
    }

    /// <summary>Called from the pointerlockchange callback.</summary>
    internal void SetCaptured(bool captured) => IsCaptured = captured;
}

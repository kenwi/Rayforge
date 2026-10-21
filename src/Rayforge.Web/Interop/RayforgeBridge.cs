using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Rayforge.Web.Components;

namespace Rayforge.Web.Interop;

/// <summary>
/// The synchronous JS -> C# surface. <c>rayforge.js</c> resolves these with
/// <c>getAssemblyExports("Rayforge.Web.dll")</c> and calls <see cref="UpdateFrame"/> once per frame.
/// </summary>
/// <remarks>
/// Every method takes the opaque canvas handle passed to <c>start</c>. Exceptions are not allowed to
/// escape: a throwing export would abort the JS frame with no overlay. <c>RayforgeCanvas.Tick</c>
/// catches game exceptions and reports them through the component.
/// </remarks>
[SupportedOSPlatform("browser")]
public static partial class RayforgeBridge
{
    /// <summary>One frame: <c>Update(dt)</c> + <c>Draw()</c>. Returns false to stop the loop.</summary>
    [JSExport]
    public static bool UpdateFrame([JSMarshalAs<JSType.Any>] object handle, float deltaTime)
        => handle is RayforgeCanvas canvas && canvas.Tick(deltaTime);

    /// <summary>Reported by the ResizeObserver and after fullscreen changes.</summary>
    [JSExport]
    public static void OnLayoutChanged(
        [JSMarshalAs<JSType.Any>] object handle,
        double hostWidth,
        double hostHeight)
    {
        if (handle is RayforgeCanvas canvas)
        {
            canvas.ApplyHostSize(hostWidth, hostHeight);
        }
    }

    /// <summary>DOM fullscreen changed (entered, exited, or rejected by the browser).</summary>
    [JSExport]
    public static void OnFullscreenChanged([JSMarshalAs<JSType.Any>] object handle, bool isFullscreen)
    {
        if (handle is RayforgeCanvas canvas)
        {
            canvas.SetFullscreen(isFullscreen);
        }
    }

    /// <summary>Pointer lock was acquired or released.</summary>
    [JSExport]
    public static void OnPointerLockChanged([JSMarshalAs<JSType.Any>] object handle, bool captured)
    {
        if (handle is RayforgeCanvas canvas)
        {
            canvas.SetPointerCaptured(captured);
        }
    }

    /// <summary>WebGL context lost or the frame export failed. The page needs a reload.</summary>
    [JSExport]
    public static void OnContextLost([JSMarshalAs<JSType.Any>] object handle, string message)
    {
        if (handle is RayforgeCanvas canvas)
        {
            canvas.HandleContextLost(message);
        }
    }
}

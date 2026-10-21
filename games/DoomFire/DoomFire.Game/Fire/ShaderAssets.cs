using Raylib_cs;

namespace DoomFire.Fire;

/// <summary>
/// Loads the desktop (GLSL 330) or browser (GLSL ES 300) shader pair from <c>assets/shaders/</c>.
/// Selection is runtime-only via <see cref="Rayforge.GameContext.IsBrowser"/>; the game assembly has no
/// <c>#if BROWSER</c>.
/// </summary>
internal static class ShaderAssets
{
    public static Shader Load(bool isBrowser, string fragmentFileName)
    {
        // Raylib-cs 8.1.0's browser-wasm archive is OpenGL ES 2.0 / WebGL 1 (#version 100),
        // not ES 3 / WebGL 2. Desktop stays on GLSL 330.
        string folder = isBrowser ? "es100" : "gl330";
        // .txt suffix: Blazor DevServer / ASP.NET StaticFiles 404 unknown MIME types
        // (.frag / .vert / .glsl). text/plain is always served; see PORTING-GUIDE.md.
        string vertexPath = $"assets/shaders/{folder}/fullscreen.vert.txt";
        string fragmentPath = $"assets/shaders/{folder}/{fragmentFileName}.txt";

        Shader shader = Raylib.LoadShader(vertexPath, fragmentPath);
        if (shader.Id == 0)
        {
            throw new InvalidOperationException(
                $"Shader failed to compile: {fragmentPath} (vertex {vertexPath}). " +
                "Check the console for GLSL errors and that the file is in MEMFS / next to the exe.");
        }

        return shader;
    }
}

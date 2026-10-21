# DoomFire assets

Shaders only. The fire heat buffer is generated at runtime; there is no texture or audio pack.

| Path | Purpose |
|---|---|
| `shaders/gl330/*.vert.txt`, `*.frag.txt` | Desktop OpenGL 3.3 (`#version 330`) |
| `shaders/es100/*.vert.txt`, `*.frag.txt` | Browser WebGL 1 / GLES 2 (`#version 100`) |

The game picks the folder at runtime from `GameContext.IsBrowser`. No `#if BROWSER`, no embedded
resources. Paths are lowercase so Linux and the MEMFS stay case-consistent.

## Why `.txt` and why ES 100

- **`.txt` suffix:** the Blazor DevServer (ASP.NET StaticFiles) returns HTTP 404 for unknown MIME
  types such as `.frag` / `.vert` / `.glsl`. `text/plain` is always served; raylib still compiles the
  GLSL from the file contents.
- **ES 100, not 300:** Raylib-cs 8.1.0's `browser-wasm` `raylib.a` is built for OpenGL ES 2.0 /
  WebGL 1. `#version 300 es` fails with `unsupported shader version 300`. The ES 100 present shader
  takes a `heatSize` uniform because GLSL ES 100 has no `textureSize()`.

Source: ported from `~/Work/DoomFire-Raylib` (embedded GLSL 330). The ES 100 variants rewrite
`in`/`out`/`texture`/`textureSize` for WebGL 1.

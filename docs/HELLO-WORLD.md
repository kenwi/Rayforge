# Hello World - specification

The first demo for Rayforge. Its job is not to be pretty; it is to prove that a portable game assembly,
a Blazor web host and a native desktop host all behave the same, and that the browser-specific pieces
(resize, mouse mapping, audio unlock, assets, errors) work.

Status: **specification**, implemented in M2. M0 already validated the pieces this demo depends on
([spike 0001](spikes/0001-raylib-cs-blazor-wasm.md)).

---

## 1. What it demonstrates

| Concern | How the demo exercises it |
|---|---|
| Static native linking | raylib 6.0 from the Raylib-cs 8.1.0 package (`runtimes/browser-wasm/native/raylib.a`) - no vendored binary |
| Game/host boundary | `HelloWorldGame` is a `net10.0` class library with zero Blazor/WASM references |
| Desktop parity | The identical game class runs in `HelloWorld.Desktop` |
| Frame loop | rAF-driven `Update(dt)` + `Draw()`; no blocking loop |
| Delta time | Rotating cube speed is frame-rate independent |
| 3D rendering | Rotating cube with `Camera3D`, depth test, colored faces |
| 2D rendering | Text, FPS, resolution, border, mouse marker |
| Texture loading | `assets/logo.png` (fetched into MEMFS at boot) |
| Font loading | `assets/ui.ttf` (custom font, falls back to default if missing) |
| Audio + autoplay policy | Click-to-start overlay, beep on click (`assets/blip.ogg`) |
| Input | Space toggles rotation, R resets, F fullscreen, mouse marker |
| Resize / letterboxing | Fit mode; mouse stays under the cursor at every size/zoom/DPR |
| Error handling | A debug key can throw on purpose to prove the crash overlay (optional) |

Not demonstrated (deliberately): pointer lock, menus, scenes, levels, save data, networking, mobile, gamepad.

---

## 2. Visual and behavior

Logical resolution: **1280 x 720**, `CanvasScaleMode.Fit`, dark background, letterboxed.

```
+------------------------------------------------------------------+
|  Hello, Rayforge!                            FPS 60  1280x720   |
|                                                                  |
|                         +--------+                               |
|                        /        /|                               |
|                       +--------+ |      <- rotating 3D cube      |
|                       |        | +                               |
|                       |        |/                                |
|                       +--------+                                 |
|                                                                  |
|   [logo]  Raylib 6.0 - Blazor WebAssembly                        |
|                                                                  |
|                                              o <- mouse marker   |
+------------------------------------------------------------------+
```

### Boot

1. Page shows a dark canvas area and a "Loading..." overlay (Blazor).
2. Assets load (Bundled mode: already in the VFS; the overlay is brief).
3. A "Click to play" button appears (because audio is enabled and `RequireUserGesture` is true).
4. On click: `InitWindow(1280, 720)`, `InitAudioDevice()`, `HelloWorldGame.Initialize`, loop starts,
   overlay disappears.
5. If any step fails: a readable error overlay with a Reload button; no silent black screen.

### Running

| Input | Effect |
|---|---|
| `Space` | Toggle cube rotation (paused/resumed) |
| `R` | Reset rotation, marker rings, camera |
| `F` | Toggle DOM fullscreen (browser) / window fullscreen (desktop) |
| Left click | Play a short beep; spawn a ring at the mouse position that fades over ~0.6 s |
| Mouse move | Draw a small circle at `GetMousePosition()`; must stay under the cursor |
| `Escape` | No default action; the demo ignores it (`SetExitKey(KeyboardKey.Null)`). Close the window/tab to exit |
| `D` (debug) | Toggle the diagnostics line (FPS, frame time, backing/CSS size, scale, asset mode) |

The mouse marker is the resize test: if it drifts from the cursor at any window size or zoom level, the
`SetMouseScale` math is wrong.

### Rendering details

- `BeginDrawing()` / `ClearBackground()` / ... / `EndDrawing()` live in `HelloWorldGame.Draw()`.
- Cube: `BeginMode3D` with a fixed camera looking at the origin; `DrawCube` + `DrawCubeWires`; cube rotates
  around Y and slightly around X; paused by Space.
- Background: very dark blue/gray, plus a subtle grid or vignette (optional).
- Text: custom font at 32 px for the title, 18 px for the footer and diagnostics; all positions relative to
  `GetScreenWidth/Height` so the desktop window can resize.
- Border: a 2 px rectangle inset by 8 px, showing the logical viewport; helps spot stretching.
- Logo: 64x64 texture drawn bottom-left with a 1:1 size (no filtering artifacts).
- Ring effect: a small array of `{position, age}`; each ring grows and fades; caps at 32 entries.
- FPS: use `Context.Diagnostics.Fps` (host-measured) or `GetFPS()`; label which one. Prefer the host value
  because raylib's timing on web is tied to `EndDrawing`.

### Audio

- `blip.ogg`: ~0.15 s, 440 Hz sine with a short fade, mono, 44.1 kHz.
- Played on left click; volume 0.4; no overlapping spam (optional: limit to ~20/s).
- If `Context.AudioAvailable == false`, clicks still spawn rings, silently.

### Resize

- Fit mode: the canvas CSS size changes; the backing buffer stays 1280x720; the game never sees a resize.
- The border and text remain proportional (they are drawn in logical pixels).
- The mouse marker must align at 50%, 100%, 200% browser zoom and at DPR 1, 1.25, 1.5, 2.
- Desktop: resizing the window triggers `OnResize`; the game redraws relative to the new size (the demo
  supports arbitrary sizes on desktop; on web it is fixed by Fit).

### Fullscreen

- Browser: `requestFullscreen()` on the canvas container, from the F keydown (a user gesture). Exiting via
  Esc updates `IsFullscreen` through `fullscreenchange`.
- Desktop: raylib `ToggleFullscreen()`.
- If the browser rejects the request, log it; do not crash.

### Teardown

- Closing the tab: browser cleans up.
- A "Quit" path is not required in the demo, but `Dispose` must unload the texture, font and sound and close
  the audio device/window. The base calls it exactly once.

---

## 3. File layout (expected)

```
games/HelloWorld/
├── Assets/
│   ├── logo.png            # 64x64, generated placeholder
│   ├── blip.ogg            # short beep
│   └── ui.ttf              # small open-license font (or reuse raylib default)
├── Assets.props
├── HelloWorld.Game/
│   ├── HelloWorldGame.cs   # ~150-200 lines, all game logic
│   └── HelloWorld.Game.csproj
├── HelloWorld.Web/
│   ├── App.razor           # <RayforgeCanvas />
│   ├── Program.cs          # AddRayforge<HelloWorldGame>(...)
│   ├── _Imports.razor
│   ├── wwwroot/index.html
│   ├── wwwroot/css/app.css
│   └── HelloWorld.Web.csproj
└── HelloWorld.Desktop/
    ├── Program.cs          # DesktopRunner.Run<HelloWorldGame>(...)
    └── HelloWorld.Desktop.csproj
```

The game project references `Rayforge.Core` and `Raylib-cs` only. It must not know that a canvas, Blazor or
WASM exist.

---

## 4. Acceptance criteria

A build is accepted when **all** of these pass:

### Web

1. `dotnet run --project games/HelloWorld/HelloWorld.Web` serves the app; Chrome and Firefox show the
   loading overlay, then "Click to play".
2. After clicking, the cube rotates smoothly, the title/footer/logo render, and the FPS readout is stable
   (no runaway loop, no busy-wait).
3. Space pauses/resumes rotation; R resets; clicking plays a beep and spawns a fading ring; the audio starts
   on the first click (no blocked-autoplay warning in the console).
4. Resizing the window letterboxes the game without stretching; the mouse marker stays under the cursor at
   50%, 100% and 200% zoom and at DPR 1 and 2.
5. F enters/exits fullscreen; Esc exits fullscreen; no exception in the console.
6. Reloading the page starts cleanly; no "window already initialized" error.
7. Publishing with `dotnet publish -c Release -o artifacts/hello-world` and serving
   `artifacts/hello-world/wwwroot` works over HTTP (not `file://`), including from a sub-path
   (`/<repo>/`).
8. No console errors during boot, run or teardown.
9. The published app loads no Wolfrender files/namespaces.

### Desktop

10. `dotnet run --project games/HelloWorld/HelloWorld.Desktop` opens a 1280x720 resizable window with the
    same visuals and behavior, at the same logical speed.
11. Resizing the desktop window updates the layout, and the mouse marker remains correct.
12. Closing the window exits cleanly; no unload/audio errors.

### Architecture

13. `HelloWorld.Game.dll` references neither `Rayforge.Web`, `Rayforge.Desktop`, `Microsoft.AspNetCore.*`
    nor `System.Runtime.InteropServices.JavaScript` (automated test).
14. `Rayforge.Core.dll` references no Blazor/JS interop assemblies (automated test).
15. The web host contains no game rules or raylib drawing calls (only configuration/DI).
16. The base contains no `HelloWorld` names, assets or key bindings.

### Error path

17. Forcing an exception inside `Update` (debug flag) stops the loop, unloads the game, and shows the error
    overlay with a Reload button; the rest of the page remains usable. No infinite console spam.

### Non-functional

18. Record: publish time, total `wwwroot` size, transferred size on first load (gzip/br), and steady-state
    memory in DevTools. These numbers go in `docs/HELLO-WORLD.md` after M2 so future regressions are visible.
19. No AOT requirement for acceptance; if AOT is enabled in Release, criteria 1-18 still pass.

---

## 5. Stretch goals (only after acceptance)

- Pointer lock demo: hold right mouse to capture, look around with a first-person camera; Esc releases.
- Integer scaling toggle for pixel-perfect Fit.
- A "Copy diagnostics" button (browser + .NET + raylib versions).
- A shader toy panel (proves GLSL ES variants).
- A second game behind a query param (not multiple games in one page; just a deploy variant).

Do not let these delay M2.

---

## 6. Implementation notes / traps

- Keep the canvas mounted and non-zero-sized behind the overlay. `InitWindow` against a `display:none` or
  0x0 canvas produces a broken GL context and a black screen.
- Call `InitAudioDevice` from the click handler **before any `await`**. If asset fetching happens after the
  click, the gesture is lost and audio may be suspended.
- `BeginDrawing`/`EndDrawing` belong to the game, not the base. Ported games already do this; keep it.
- Do not call `SetTargetFPS` on web. The host paces; rAF caps at the display refresh.
- Do not use `Thread.Sleep` for the ring fade; use `dt`.
- Do not recreate the canvas or re-init raylib on a Blazor re-render. `OnAfterRenderAsync(firstRender)` and a
  `_started` guard.
- Use `Raylib.GetScreenWidth/Height` for layout inside `Draw`; do not cache them at init if the desktop window
  can resize.
- Unload every `Texture2D`, `Font`, `Sound` you load. The demo should be leak-free across a full page reload
  (DevTools memory snapshot before/after is enough).
- Keep the game class free of `#if BROWSER`. If a platform branch is truly needed, put it behind a
  `Context.IsBrowser` check or a base service, not a compile-time symbol, so the same assembly can be tested
  on desktop.
- The demo font should have a clear license (SIL OFL) and be credited in `Assets/README.md`.

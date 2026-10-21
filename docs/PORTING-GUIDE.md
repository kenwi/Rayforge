# Porting a Raylib-cs game to Rayforge

Draft guide. It will be validated in M3 by porting DoomFire-Raylib, then in M6 by porting Wolfrender.
Status: **not yet tested against a real port**.

---

## 0. What "port" means here

Rayforge does **not** translate code or emulate a desktop. It changes the *host* of a C#/Raylib game:

- the game becomes a `net10.0` class library implementing `IGame`;
- a Blazor WASM host owns the canvas, the window, audio and the frame loop;
- the same game library is also referenced by a tiny desktop host.

The rendering, input polling, asset loading and game logic stay ordinary Raylib-cs code. The work is mostly:

1. remove the blocking loop and window lifecycle from the game;
2. move asset paths into the virtual filesystem convention;
3. replace a small set of desktop-only APIs (cursor, fullscreen, threads);
4. verify input/mouse scaling and shaders.

Expect the first port to take hours, not weeks, if the game has no server, editor or threading. Wolfrender
is an outlier because it also needs a raylib 5.5 -> 6.0 API upgrade and has a large editor/server surface.

---

## 1. The 30-minute port (small game)

### Step 1 - Create the game library

```
games/<Name>/
├── <Name>.Game/
│   ├── <Name>Game.cs
│   └── <Name>.Game.csproj       net10.0
├── <Name>.Web/                  Blazor WASM host
├── <Name>.Desktop/              console host
└── Assets/
```

`<Name>.Game.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\src\Rayforge.Core\Rayforge.Core.csproj" />
    <PackageReference Include="Raylib-cs" Version="7.0.2" />
  </ItemGroup>
</Project>
```

No Blazor, no `System.Runtime.InteropServices.JavaScript`, no `#if BROWSER`.

### Step 2 - Split `Main`

Original:

```csharp
static void Main()
{
    InitWindow(1280, 720, "My Game");
    InitAudioDevice();
    var texture = LoadTexture("resources/player.png");
    var player = new Player();

    while (!WindowShouldClose())
    {
        var dt = GetFrameTime();
        player.Update(dt);
        BeginDrawing();
        ClearBackground(Color.Black);
        DrawTexture(texture, 10, 10, Color.White);
        EndDrawing();
    }

    UnloadTexture(texture);
    CloseAudioDevice();
    CloseWindow();
}
```

Ported:

```csharp
public sealed class MyGame : GameBase
{
    private Texture2D _texture;
    private Player _player = new();

    protected override void OnInitialize()
    {
        _texture = LoadTexture("assets/player.png");
        _player = new Player();
    }

    public override void Update(float dt)
    {
        _player.Update(dt);
    }

    public override void Draw()
    {
        BeginDrawing();
        ClearBackground(Color.Black);
        DrawTexture(_texture, 10, 10, Color.White);
        EndDrawing();
    }

    public override void Dispose()
    {
        UnloadTexture(_texture);
    }
}
```

Key mechanical rules:

- **Setup** -> `OnInitialize` (window/audio removed).
- **Loop body** -> `Update` + `Draw`. Keep `BeginDrawing`/`EndDrawing` in `Draw`.
- **Cleanup** -> `Dispose`.
- **Loop-spanning locals** (textures, player, camera, timers) -> fields.
- **`WindowShouldClose`** -> removed. The host stops the loop; use `Context.Runtime.RequestQuit()` if the
  game has its own "quit to menu" logic.
- **`SetTargetFPS`** -> removed; use `GameOptions.TargetFps` (web paces in JS; desktop can use vsync).
- **`GetFrameTime`** -> use the `dt` argument (host-provided, clamped). It is consistent across hosts.
- **Global mutable state** -> move into the game class. One game instance per page.

### Step 3 - Register the hosts

Web `Program.cs`:

```csharp
builder.Services.AddRayforge<MyGame>(o =>
{
    o.Title = "My Game";
    o.Width = 1280;
    o.Height = 720;
    o.ScaleMode = CanvasScaleMode.Fit;
    o.EnableAudio = true;
    o.RequireUserGesture = true;
});
```

`App.razor`: `<RayforgeCanvas />`.

Desktop `Program.cs`:

```csharp
DesktopRunner.Run<MyGame>(new()
{
    Title = "My Game",
    Width = 1280,
    Height = 720,
    EnableAudio = true,
});
```

### Step 4 - Assets

- Move every runtime file into `games/<Name>/Assets/`, preserving subfolders.
- Replace `resources/...`, absolute paths, `AppContext.BaseDirectory`, etc. with `assets/...`.
- Add `Assets.props` (see `PLAN.md` section 4.6) and import it from both hosts.
- Test the desktop host first: it proves the paths and the game logic without browser variables.
- On web, start with **Bundled** mode. Switch to **Preload** only if startup time/memory demands it.

### Step 5 - Run desktop, then web

1. Desktop: fastest feedback; breakpoints work normally.
2. Web: `dotnet run --project ...Web`, open the browser, check the console for the first error.
3. Resize and move the mouse over a known point; verify the marker lines up.
4. Play through the game; watch memory and FPS.

---

## 2. Platform difference checklist

Go through this list before declaring the port done.

### Lifecycle

- [ ] `InitWindow`, `CloseWindow`, `InitAudioDevice`, `CloseAudioDevice` removed from the game.
- [ ] No `while (!WindowShouldClose())` anywhere.
- [ ] No `Environment.Exit`.
- [ ] `Dispose` unloads every raylib resource (textures, fonts, sounds, music, shaders, render textures,
      models).
- [ ] The game can be constructed and initialized once per page; no static state that survives a reload.

### Timing

- [ ] No `Thread.Sleep`, `Task.Delay` in the loop, or busy-wait.
- [ ] All movement/animation uses `dt`; speeds are per second.
- [ ] `dt` is clamped by the host; do not assume a fixed 16.6 ms.
- [ ] No `SetTargetFPS` on web.
- [ ] Timers use accumulated `dt` or `Stopwatch`, not frame counts.

### Threads and async

- [ ] No `Thread`, `Task.Run`, `Parallel.For`, `lock` contention assumptions. The WASM runtime is
  single-threaded by default.
- [ ] Long loads use async + a loading state, not a blocking wait.
- [ ] `async void` is not used for game logic.

### Input

- [ ] The canvas gets focus (click) before keyboard input works.
- [ ] Space/arrows/Tab do not scroll the page while playing (base handles this; verify).
- [ ] Mouse coordinates are correct under Fit scaling (base handles `SetMouseScale`; verify).
- [ ] Pointer lock: use `Context.MouseCapture`, not `DisableCursor`.
- [ ] `GetMouseDelta` under pointer lock: use `Context.MouseCapture.ConsumeDelta()` if the game is an FPS.
- [ ] Browser-reserved shortcuts are not swallowed.
- [ ] Gamepad/touch are either unsupported by design or tested.

### Rendering

- [ ] Shaders use GLSL ES (`#version 100` or `300 es`) on web; desktop GLSL 330 variants are behind a
      runtime/compile branch.
- [ ] No `#if BROWSER` in the game assembly if avoidable; prefer a uniform shader or a runtime choice.
- [ ] Render textures / `BeginTextureMode` work at the logical size; check for upside-down output on web.
- [ ] `GetScreenWidth/Height` is read when drawing or after `OnResize`, not cached forever.
- [ ] Fullscreen uses `Context.Runtime.ToggleFullscreen`, not `ConfigFlags`.
- [ ] Screenshots (if any) are downloaded via JS; MEMFS files disappear.

### Audio

- [ ] Audio starts after a user gesture (`RequireUserGesture=true`).
- [ ] `InitAudioDevice` failure is handled (`Context.AudioAvailable`).
- [ ] Music/sounds are preloaded; no streaming from HTTP.
- [ ] OGG/WAV/MP3 only; check licenses and file sizes.
- [ ] No audio-thread assumptions (callbacks) unless tested on web.

### Assets and IO

- [ ] All paths are lowercase `assets/...`, relative, no leading slash.
- [ ] No `Directory.GetFiles`, `File.Exists`, `File.WriteAllBytes` on arbitrary paths. `File.Exists` may work
      in the VFS; arbitrary directories do not.
- [ ] Writes go to MEMFS (lost on reload) or browser storage via JS.
- [ ] Asset sizes are known; initial memory stays within `INITIAL_MEMORY` + growth.
- [ ] Fonts are licensed and loaded via `assets/...`.

### Data and .NET

- [ ] No reflection-heavy serialization that breaks under trimming/AOT; prefer `System.Text.Json` source
      generation.
- [ ] No `System.Reflection.Emit`, dynamic assemblies, or plugins loaded from disk.
- [ ] `DateTime.Now`/`Stopwatch` are fine.
- [ ] `System.Net.Http` calls are CORS-safe and use the host's base address for same-origin assets.
- [ ] Culture-sensitive parsing is avoided or `InvariantGlobalization` is accounted for.

### UX

- [ ] A loading state exists (bundled assets can still take time).
- [ ] A click-to-start overlay exists if audio/pointer lock is used.
- [ ] Errors show a readable overlay with Reload, not a black canvas.
- [ ] The page does not scroll/select text when the game is focused.
- [ ] Mobile is either supported deliberately or shows a "desktop recommended" message.

---

## 3. Worked example: DoomFire-Raylib (M3)

`~/Work/DoomFire-Raylib` is a small raylib game/effect with a shader. It is the first real port because it
exercises:

- `InitWindow` + a game loop;
- shader loading (GLSL variants);
- a full-screen quad / render texture;
- timing and input;
- no server, editor or large asset set.

Expected work:

1. Move `Fire/` and `App/DoomFireHost.cs` into `DoomFire.Game`.
2. Convert `DoomFireHost`'s loop into `IGame`.
3. Move shaders to `Assets/shaders/`; add a GLSL ES variant.
4. Add the web/desktop hosts.
5. Record every friction point in `docs/spikes/` and update this guide.

If DoomFire needs fewer than ~50 lines of host-specific changes, the architecture is doing its job.

---

## 4. Worked example: Wolfrender (M6, large)

Do not start here. Wolfrender is a full game with an editor, server, custom shaders, 5 MB of assets and a
raylib 5.5-era API. It is the stress test, not the onboarding test.

Known work items:

| Area | Change |
|---|---|
| Raylib version | Same 7.0.2/raylib 5.6-dev baseline in v1; the 8.1.0/raylib 6.0 upgrade is M7 |
| Project split | `Source/Core`, `Engine`, `Features` -> `Wolfrender.Game`; remove `Components/Raylib*`, pages, JS |
| Lifecycle | `Application`/`World` loop -> `IGame`; `InitWindow`/`CloseWindow` out |
| Assets | `wwwroot/resources` (5.1 MB) -> `Assets/`; hardcoded preload list -> manifest/groups |
| Browser bridge | `BrowserPointerLockBridge`, `GraphicsFramePacing` -> `Context.MouseCapture`, host pacing |
| Editor | `Source/Editor`, `rlImGui-cs`, `WebEditor*` -> out of the web build; decide whether to keep a
  desktop-only editor or drop it |
| Server | `Server/` highscores -> out of scope for a static client; keep as an external service if needed |
| Shaders | `resources/shaders/*` -> GLSL ES variants |
| Input | verify mouse sensitivity, pointer lock, Escape/Backspace behavior, non-US layouts |
| UI | options/debug console overlays -> decide: raylib-drawn (portable) or Blazor HTML (web-only) |
| Tests | `World`/systems can be unit-tested as a plain library once the split is done |

Recommended order: split the game into a library with no Blazor references *first* (keeping 7.0.2), get the
desktop host running, then add the web host. Do not combine the split with the M7 raylib upgrade.

---

## 5. Common failures and fixes

| Symptom | Cause | Fix |
|---|---|---|
| `Cannot find native library 'raylib'` / unresolved symbol | RID not `browser-wasm`, `NativeFileReference` missing, or `WasmBuildNative` false | Set `RuntimeIdentifier=browser-wasm`, `WasmBuildNative=true`, `WasmAllowUndefinedSymbols=true`; make sure the framework props are imported; check `obj/.../wasm/for-build/*.rsp` |
| Link warnings for `DrawLineBezierCubic`, `DrawLineBezierQuad`, `GetSoundsPlaying` | Raylib-cs 7.0.2 bindings vs the 5.6-dev `raylib.a` | Harmless unless called; rebuild `raylib.a` from the matching commit, or wait for M7 |
| `wasm-opt: Unknown option '--enable-bulk-memory-opt'` | Release link with Binaryen 117 (spike 0001, 4.3) | `EmccLinkOptimizationFlag=-O0` (temporary); try `dotnet workload update`; remove system EMSDK from `PATH` |
| Black canvas, no errors | Canvas not bound before `InitWindow`, or `Module.canvas` is the wrong module | Bind via `Blazor.runtime.Module['canvas']` in `OnAfterRenderAsync`; verify `canvas.id` |
| Black canvas after hot reload | raylib window already initialized | Disable hot reload; full page reload; guard second init |
| `GET /_framework/js/...` 404 / dynamic import fails | `JSHost.ImportAsync` resolves relative URLs against `_framework/` | Pass an absolute URL built from `NavigationManager.BaseUri` |
| Keyboard does nothing | Canvas not focused | `tabindex=0`, focus on click; check Emscripten GLFW listeners |
| Mouse marker offset | CSS scaling without `SetMouseScale`, or DPR mismatch | Fit mode + `SetMouseScale(backing/css)`; test at DPR 1/2 and zoom |
| Audio silent / autoplay warning | `InitAudioDevice` before a gesture | Click-to-start; init audio synchronously in the click handler |
| Assets 404 / `LoadTexture` fails | Not written into MEMFS, wrong case, leading slash | Fetch into MEMFS at boot (spike 0001, 4.1); lowercase relative `assets/...` |
| DevServer serves an asset with HTTP 200 and 0 bytes | `Content` item with `Link="wwwroot\..."` but no physical file there | Use the copy-target recipe (spike 0001, 4.2); do not mix it with `Content ... Link` |
| `InitWindow` works in Debug, crashes in Release | AOT/trimming removed something; exception handling | Enable `WasmEnableExceptionHandling`; add trim roots; test AOT separately |
| A click never registers | Press and release land in the same frame; raylib samples button state per frame | Hold the button >= 1 frame; do not build UI on sub-frame taps |
| Extremely slow Debug | AOT forced on in Debug | `RunAOTCompilation` only in Release |
| Frame rate pegged at 100% CPU | raylib `SetTargetFPS` busy-wait on web | Remove it; let rAF pace; host `TargetFps` |
| Huge download / OOM | All assets bundled + decoded | Preload groups; compress audio; reduce texture sizes; raise initial memory |
| Sub-path deploy breaks JS/assets | Absolute URLs | `<base href="./">`; `_content/...` with `./`; relative assets |
| `Task.Run` never completes / UI freezes | Single-threaded WASM | Remove threads; use async state machines |
| `File.WriteAllText` "works" but data vanishes | MEMFS is in-memory | Persist via browser storage/download; do not rely on MEMFS |

---

## 6. Porting scorecard

Use this to decide whether a game is a good candidate:

| Question | Good sign | Red flag |
|---|---|---|
| Does it use a blocking game loop? | Easy to invert | Deeply nested loops/state machines |
| Does it use threads? | None | Worker threads, locks, `Task.Run` in gameplay |
| Does it use the filesystem? | Reads known assets | Scans directories, writes saves, plugins |
| Does it use native libraries? | Only raylib | Custom C libs via P/Invoke |
| Does it use a server? | No | Required backend/auth |
| Shaders | Simple / portable | Desktop GLSL 330 only, many variants |
| UI | raylib-drawn | ImGui, native dialogs, OS integration |
| Assets | < 20 MB | Hundreds of MB, streaming video |
| Input | Keyboard/mouse | Gamepad-only, complex touch |
| Resolution | Fixed or simple | Multi-monitor/DPI-dependent layout |
| Platform APIs | None | Clipboard, notifications, file drag-drop, OS paths |

A game with one or two red flags is still portable; expect targeted work. A game with five is a project, not
a port.

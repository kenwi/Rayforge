# Rayharbor - Architecture & Implementation Plan

> Status: **draft for review**. Nothing is implemented.
> Date: 2026-10-21
> Repository: `~/Work/Rayharbor` (working name, no remote yet)

---

## 0. TL;DR - decisions at a glance

| # | Question | Recommendation |
|---|---|---|
| 1 | Name | **Rayharbor** (working name). Alternatives in section 3. |
| 2 | Structure | **Layered monorepo**: `src/` framework + `games/<Game>/` with `.Game`, `.Web`, `.Desktop`. |
| 3 | Base + game separation | The game is a **plain `net10.0` class library**; the Blazor app is a **thin host**. Do not copy the base into each game. |
| 4 | Raylib baseline | **Raylib-cs 8.1.0** (raylib 6.0). It now ships official `browser-wasm` support and `raylib.a`; no vendored native library is needed. |
| 5 | Who owns the window/loop | The **host** owns `InitWindow` / `InitAudioDevice` / the frame loop. The game implements `IGame` (`Initialize`, `Update`, `Draw`, `Dispose`). |
| 6 | Frame loop | `requestAnimationFrame` in JS calls **one synchronous `[JSExport]`** per frame. No blocking `while` loop, no per-frame `async Task`, no per-frame JS interop. |
| 7 | Canvas sizing | Default **Fit** (fixed logical resolution, CSS letterboxing, `SetMouseScale`). Optional **Fill** (backing store = CSS pixels, responsive). HiDPI is a later mode. |
| 8 | Assets | Default **Bundled** (`WasmFilesToIncludeInFileSystem`). Add **Preload** (manifest + fetch + progress) before porting a large game. |
| 9 | Desktop parity | **In scope from day one**, via a thin `Rayharbor.Desktop` runner. It is the cheapest proof that the game code is portable. |
| 10 | Hello World | Rotating 3D cube + 2D text + texture + click sound + mouse marker + resize + fullscreen. Same game on web and desktop. |
| 11 | Out of scope | Engine/ECS, editor, ImGui, level format, server, networking, hot reload, mobile controls. |

**Highest-risk item:** Raylib-cs 8.1.0's browser-wasm support is new and its official example is *not* Blazor.
Milestone **M0** is a one-day spike that must prove it works in a Blazor app before anything else is built.
Fallback: the Wolfrender-proven Raylib-cs 7.0.2 + custom `raylib.a` setup.

---

## 1. What `Wolfrender.Blazor.Raylib` does today

### 1.1 Runtime path (as proven)

1. A Blazor WebAssembly app (`Microsoft.NET.Sdk.BlazorWebAssembly`) renders a full-viewport `<canvas>`.
2. `Components/Raylib.razor.cs` imports `wwwroot/js/raylib.js` with `JSHost.ImportAsync`, then binds the
   Emscripten module to the canvas: `Blazor.runtime.Module['canvas'] = canvas`.
3. `InitWindow(width, height, title)` is called from C#. The raylib native library is statically linked into
   the .NET wasm module via `NativeFileReference Include="raylib.a"` and `WasmBuildNative=true`
   (`Raylib.props`). The committed `raylib.a` is a 1.6 MB Emscripten build (`5.6-dev`, GLFW backend).
4. Frames are driven from JS: `requestAnimationFrame` calls a `[JSExport]` method, which invokes the page's
   `OnRender(float delta)` callback. An optional alternative uses `emscripten_set_main_loop`.
5. Game code calls raylib directly (`BeginDrawing`, textures, shaders, audio, input), exactly as on desktop.
6. Assets are fetched by JS and written into the Emscripten MEMFS (`preloadFile`) before use.
7. Pointer lock, resize, audio and frame pacing are coordinated through ad-hoc static bridges
   (`BrowserPointerLockBridge`, `GraphicsFramePacing`).

The desktop build reuses the same game sources by compiling the `Source/**/*.cs` files into a console
project with `CONSOLE_APP` defined (`Wolfrender.Blazor.Raylib.Console.csproj`). This works, but it is a
source-level fork, not a real library boundary.

### 1.2 What is worth keeping

- **The core trick works.** A Blazor canvas + statically linked raylib + C# game code is viable and fast.
- **Host-owned initialization.** `InitWindow` / `InitAudioDevice` in the page, game code independent of boot.
- **Direct Raylib-cs usage.** The game is normal raylib code; no wrapper API to learn. This is essential for
  porting existing games and should not change.
- **JS-driven frame loop.** rAF + a single C# entry point is the right shape for the browser.
- **Asset preloading into MEMFS.** raylib's file loaders are synchronous, so assets must be in the virtual
  filesystem before the game asks for them.
- **HTML/Blazor overlays.** Menus, settings, loading and error UI are much easier in Razor than in raylib.
- **Full-viewport canvas, pointer lock, resize handling.** These are real browser problems worth solving once.
- **Static hosting on GitHub Pages** with `404.html`, `.nojekyll` and a custom domain is a proven deployment.

### 1.3 What we should not carry over

| Existing trait | Why it hurts a reusable base |
|---|---|
| Base component references `Game.Engine.Input.BrowserPointerLockBridge` | The framework depends on one game's namespace. Inverts the dependency direction. |
| JS hardcodes `Wolfrender.Blazor.Raylib.dll` and `"canvas"` | One canvas, one assembly name; breaks as soon as the base is renamed or used twice. |
| `JSHost.ImportAsync("Raylib", "../js/raylib.js")` | Path depends on the host's folder layout; not an RCL static asset. |
| `OnInitializedAsync` starts raylib before the first render | Relies on an `await` yielding to the renderer; fragile. Use `OnAfterRenderAsync`. |
| `[JSExport] async Task EventAnimationFrame` | Allocates a `Task`/promise per frame; unnecessary. Use a synchronous `bool`/`void`. |
| `getExports()` awaited inside the loop | Adds async overhead and a failure point every frame; cache once. |
| Vitest? None; no tests at all | Browser bridge, resize math and porting rules need tests. |
| `RunAOTCompilation=true` always | Debug builds become slow; AOT should be opt-in/Release. |
| Committed `raylib.a`, no build script/version pin | Undocumented binary; 5.6-dev vs Raylib-cs 7.0.2 (raylib 5.5) ABI drift. |
| Game + host + server in one repo/project | `Compile Include` duplication for desktop; no package boundary; base fixes are copied. |
| Hardcoded asset list in the page | Every port edits host code; no progress, no grouping. |
| No error boundary around the loop | One game exception kills the page with a black canvas. |
| Static pointer-lock state | Only one game/canvas per page is possible; state leaks across reloads. |
| `canvas.width = clientWidth` while passing `dpr` | HiDPI is ignored; blurry rendering and mismatched mouse coordinates. |

**Conclusion:** keep the technique, drop the coupling. Rayharbor is a rewrite of the bridge around an explicit
game contract, not a refactor of Wolfrender.

---

## 2. Goals and non-goals

### 2.1 Goals

1. **Port a C#/Raylib game to the browser with mechanical changes**, not a rewrite:
   - move `Main`'s setup/loop body/cleanup into `Initialize/Update/Draw/Dispose`,
   - make asset paths relative,
   - replace window/cursor/threading APIs with the provided services.
2. **One game library, two hosts.** The same `net10.0` game assembly runs in Blazor WASM and in a native
   Raylib window.
3. **A base with no game knowledge.** No kingdom-specific namespaces, no hardcoded assembly names, no
   hardcoded canvas IDs, no game assets.
4. **Boring, debuggable browser behavior:** visible loading/error states, correct resize and mouse mapping,
   audio unlock, clean teardown.
5. **Static deployment.** `dotnet publish` produces a folder that can be served by GitHub Pages, Netlify,
   nginx or any static host, including under a sub-path.
6. **Cheap to start a new game.** A `dotnet new` template by M4; copying a sample folder before that.
7. **Testable core.** Pure layout/timing/porting rules are unit-tested without a browser; the published app
   gets a Playwright smoke test.

### 2.2 Non-goals (for v1)

- A game engine: no ECS, scene graph, physics, asset database, level format or editor.
- ImGui / `rlImGui` integration. The desktop editor stack is Wolfrender-specific.
- A server, leaderboard, accounts, networking or persistence. The output is a static client.
- Hot reload of game code while a raylib window is alive. Rebuild + full page reload.
- Mobile/touch controls and Safari-specific polish (tested, but not a first-class target).
- Running several games in one page. One raylib context per page.
- Porting games written against other engines/languages. The input is C# + Raylib-cs.
- Native AOT or desktop-only packaging.

### 2.3 Why Blazor at all?

Blazor costs payload and startup time compared with a plain .NET WASM app (the official Raylib-cs example
uses `dotnet.js` + a hand-written `main.js`). It buys:

- HTML/Blazor overlays for loading, start, errors, settings and menus without writing a JS UI layer;
- DI, configuration and C# event handling for the non-game parts;
- the existing Wolfrender experience and the user's familiarity;
- a clean place to host per-game services (asset loader, pointer lock, fullscreen).

The game loop must **not** depend on Blazor rendering. Blazor updates only at boot, on errors and on
user-driven overlay changes. If the UI framework ever becomes a problem, `Rayharbor.Core` + the JS bridge
can be reused from a plain WASM host.

---

## 3. Name

### 3.1 Recommendation: **Rayharbor**

- "Ray" ties it to raylib; "harbor" is where things are brought in / docked - a port for games.
- Reads well as a namespace and package prefix: `Rayharbor.Core`, `Rayharbor.Web`, `Rayharbor.Desktop`.
- No GitHub repositories named `rayharbor` at the time of writing (GitHub search: 0 hits). NuGet/domain
  availability must still be checked before publishing.
- Tagline: *"Dock your Raylib game in the browser."*

### 3.2 Alternatives

| Name | Meaning / why | Risk / why not |
|---|---|---|
| **Rayharbor** | Raylib + harbor (port). Unique, brandable, works in code. | Slightly abstract; must check NuGet/trademark. |
| **Slipway** | A ramp for launching boats - launching games into the web. Short. | 136 GitHub repos; generic outside the metaphor. |
| **Rayport** | Most literal: raylib + port. | `atarigo/rayport` already packages Python raylib games for the web; confusing collision. |
| **Porthole** | A window into the game; friendly. | Sounds like a UI/widget library, not a runtime. |
| **Rayforge** | Workshop where a game is re-forged for the web. | Implies an engine/editor; close to Wolfrender's branding. |
| **Raylib.Web** | Maximum clarity and discoverability. | No brand; collides conceptually with `Blazor.Raylib`; "Blazor" is a Microsoft trademark best kept out of package IDs. |
| **Dockyard** | Where ships are fitted out. | Generic; no raylib signal. |

### 3.3 Rename strategy

Only documentation exists, so renaming costs nothing now. Later it is:
`git mv` the root folder + solution + `src/Rayharbor.*` projects, then a global replace of `Rayharbor`.
Package IDs are placeholders until M4, so no NuGet history is at stake.
**Decide the name before M1**, because project names and namespaces are easiest to set once.

---

## 4. Repository and project structure

### 4.1 On the proposed structure

The suggested layout:

```
./<Blazor-raylib-basecode>
./<source code of the actual game>
```

is the right instinct: separate the framework from the game. Two refinements:

1. **Split the host out of the game.** A game is not a Blazor app. The game is a class library; the Blazor
   app is a host that references it. This is what makes desktop parity and reuse possible.
2. **Do not copy the base into every game.** A copy diverges: bug fixes, build flags and raylib version pins
   stop flowing. Use a project reference inside the monorepo and a NuGet package for external games.

Also: name folders by **role**, not by technology. `Blazor-raylib-basecode` will be wrong the day the bridge
is used from a non-Blazor host or raylib-cs changes; `Rayharbor.Web` will still be right.

### 4.2 Structure alternatives

#### Alternative A - Layered monorepo (recommended)

```
Rayharbor/
├── src/Rayharbor.Core/          # portable contracts, no Blazor
├── src/Rayharbor.Web/           # Blazor RCL: canvas, JS bridge, browser services
├── src/Rayharbor.Desktop/       # optional desktop runner
├── games/HelloWorld/
│   ├── HelloWorld.Game/         # the portable game
│   ├── HelloWorld.Web/          # thin Blazor host
│   └── HelloWorld.Desktop/      # thin console host
├── tests/
├── templates/
└── docs/
```

**Pros**

- Hard boundary: the game cannot accidentally use Blazor/JS (enforced by project references and a test).
- One base, many games; fixes and version pins flow.
- Desktop host is a first-class, cheap proof of portability.
- The base can become a NuGet package later without moving code.
- Hosts stay tiny and game-specific configuration lives in one `Program.cs`.

**Cons**

- Three projects per game (plus three framework projects) feels heavy for a 100-line demo.
- Asset sharing needs a small `.props` file (section 4.6).
- Framework authors must test the RCL/package path, not just a single app.

**Best when:** the goal is a reusable base and more than one game (this project).

#### Alternative B - Base + game app (the literal suggestion)

```
Rayharbor/
├── framework/                   # RCL or copied base
└── MyGame/                      # Blazor WASM app containing game code
```

**Pros**

- Two projects, one `dotnet run`, minimal ceremony.
- Easy to understand; good for a single game or a quick prototype.
- The framework is still separated at the source level.

**Cons**

- Game code compiles into the Blazor assembly; Blazor/JS APIs are always in scope.
- No desktop host without the `Compile Include` trick (Wolfrender's approach).
- Upgrading the base means merging/copying; no version boundary.
- The "game" cannot be unit-tested as a plain library without dragging Blazor in.

**Best when:** exactly one game, no desktop target, and speed matters more than longevity.

#### Alternative C - NuGet-first, game repo standalone

```
MyGame/
├── src/MyGame.Game/
├── src/MyGame.Web/
├── Directory.Packages.props     # Rayharbor.Web 0.x
└── nuget.config                 # local feed during development
```

**Pros**

- Cleanest consumer story: no framework source in the game repo.
- Versioned base, reproducible builds, `dotnet new` template fits naturally.
- Games can live in separate repos/teams.

**Cons**

- Requires a package release/local feed for every base change; slow inner loop.
- Debugging across a package boundary (JS, MSBuild props, native link) is harder.
- Premature before the API settles; package versioning becomes a burden.

**Best when:** the base is stable (after M4) and games are external.

#### Alternative D - Single Blazor project (Wolfrender style)

```
MyGame/
├── Program.cs, App.razor
├── Components/Raylib...
└── Game/...
```

**Pros**

- One project, one build, zero boundaries; fastest possible start.
- Exactly what already works in Wolfrender.

**Cons**

- The coupling we are trying to escape: base and game evolve together, base cannot be reused.
- Desktop support requires compiling the same sources into a second project.
- Global statics, hardcoded names and one canvas; tests are awkward.

**Best when:** a throwaway prototype, or a fork that will never be reused.

#### Alternative E - One host, many games (launcher/gallery)

```
Launcher.Web/  ->  games/A, games/B, games/C
```

**Pros**

- One deployment; nice showcase; shared shell/menus.

**Cons**

- raylib has one global window/context; games cannot run simultaneously and switching requires teardown.
- Memory from a previous game is hard to reclaim; one crash affects the gallery.
- All assets shipped together; no per-game deploy or versioning.
- Adds lifecycle complexity for no shipping benefit.

**Best when:** a demo gallery, not a game release. Keep it as a possible `samples/` after v1.

### 4.3 Recommendation

**Alternative A now, evolving into Alternative C at M4.**

- During development, `ProjectReference` gives a fast inner loop and lets us debug the bridge.
- All three framework projects are `IsPackable=true` from day one; at M4 we pack them and the template
  consumes the package. No code moves.
- Alternative B is acceptable only if the user wants the absolute minimum and never ports a second game.
- Avoid D entirely for this project; it is the Wolfrender trap.

### 4.4 Proposed tree (to be created in M1)

```
Rayharbor/
├── Rayharbor.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
├── .gitignore
├── README.md
├── docs/
│   ├── PLAN.md
│   ├── HELLO-WORLD.md
│   ├── PORTING-GUIDE.md
│   └── spikes/                     # M0 results go here
├── src/
│   ├── Rayharbor.Core/
│   │   ├── IGame.cs
│   │   ├── GameBase.cs
│   │   ├── GameContext.cs
│   │   ├── GameOptions.cs
│   │   ├── Diagnostics.cs
│   │   ├── IMouseCapture.cs
│   │   ├── IRuntimeHost.cs
│   │   ├── IGameLog.cs
│   │   └── Rayharbor.Core.csproj            net10.0
│   ├── Rayharbor.Desktop/
│   │   ├── DesktopRunner.cs
│   │   ├── DesktopMouseCapture.cs
│   │   ├── DesktopRuntimeHost.cs
│   │   └── Rayharbor.Desktop.csproj         net10.0
│   └── Rayharbor.Web/
│       ├── Components/
│       │   ├── RayharborCanvas.razor
│       │   ├── RayharborCanvas.razor.cs
│       │   └── RayharborCanvas.razor.css
│       ├── Interop/RayharborBridge.cs
│       ├── BrowserMouseCapture.cs
│       ├── BrowserRuntimeHost.cs
│       ├── CanvasLayout.cs                  # pure, unit-testable
│       ├── ServiceCollectionExtensions.cs
│       ├── wwwroot/rayharbor.js
│       ├── buildTransitive/Rayharbor.Web.props
│       └── Rayharbor.Web.csproj             net10.0 RCL
├── games/
│   └── HelloWorld/
│       ├── Assets/
│       │   ├── logo.png
│       │   ├── blip.ogg
│       │   └── ui.ttf
│       ├── Assets.props
│       ├── HelloWorld.Game/
│       │   ├── HelloWorldGame.cs
│       │   └── HelloWorld.Game.csproj       net10.0
│       ├── HelloWorld.Web/
│       │   ├── App.razor
│       │   ├── Program.cs
│       │   ├── _Imports.razor
│       │   ├── wwwroot/index.html
│       │   ├── wwwroot/css/app.css
│       │   └── HelloWorld.Web.csproj        Blazor WASM
│       └── HelloWorld.Desktop/
│           ├── Program.cs
│           └── HelloWorld.Desktop.csproj    net10.0 Exe
├── tests/
│   ├── Rayharbor.Tests/                     # unit + architecture tests
│   └── HelloWorld.BrowserTests/             # Playwright smoke (M5)
├── templates/rayharbor-game/                # M4
└── .github/workflows/
    ├── ci.yml
    └── deploy-hello-world.yml               # M5
```

### 4.5 Project responsibilities and dependency rules

```
HelloWorld.Game  ->  Rayharbor.Core, Raylib-cs
HelloWorld.Web   ->  HelloWorld.Game, Rayharbor.Web
HelloWorld.Desktop -> HelloWorld.Game, Rayharbor.Desktop

Rayharbor.Web     ->  Rayharbor.Core, Raylib-cs, Microsoft.AspNetCore.Components.Web
Rayharbor.Desktop ->  Rayharbor.Core, Raylib-cs
```

Rules, enforced by a unit test inspecting referenced assemblies:

- `Rayharbor.Core` must not reference Blazor, JavaScript interop or WASM-specific APIs.
- `<Game>.Game` must not reference `Rayharbor.Web`, `Rayharbor.Desktop`, `Microsoft.AspNetCore.*` or
  `System.Runtime.InteropServices.JavaScript`.
- Hosts contain **no game rules**: config, asset wiring, DI, nothing else.
- The base contains **no game knowledge**: no namespaces, asset names, scene types or key bindings from a
  specific game.
- One raylib context per page. The base guards against a second `RayharborCanvas` starting.

### 4.6 Where assets live

`games/<Name>/Assets/` is the single source of truth for both hosts. A tiny `Assets.props` imported by both
host projects maps the same files into each output:

**Desktop**

```xml
<ItemGroup>
  <Content Include="$(GameAssetsDir)**\*"
           Link="assets\%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**Web, Bundled mode (default)**

```xml
<ItemGroup Condition="'$(RayharborAssetMode)' == 'Bundled'">
  <WasmFilesToIncludeInFileSystem Include="$(GameAssetsDir)**\*"
                                  TargetPath="assets\%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

**Web, Preload mode**

```xml
<ItemGroup Condition="'$(RayharborAssetMode)' == 'Preload'">
  <Content Include="$(GameAssetsDir)**\*"
           Link="wwwroot\assets\%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

Use lowercase `assets/` in code on every platform. On Linux, path case matters. The game must only use
relative paths (`assets/logo.png`), never a leading slash or an absolute path.

### 4.7 What each host looks like

**`HelloWorld.Web/Program.cs`**

```csharp
using HelloWorld;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Rayharbor.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddRayharbor<HelloWorldGame>(options =>
{
    options.Title = "Hello, Rayharbor!";
    options.Width = 1280;
    options.Height = 720;
    options.ScaleMode = CanvasScaleMode.Fit;
    options.EnableAudio = true;
    options.RequireUserGesture = true;
});

await builder.Build().RunAsync();
```

**`HelloWorld.Web/App.razor`** - no router, no layout:

```razor
<RayharborCanvas />
```

No router is deliberate: navigation would dispose/recreate the canvas and raylib cannot re-initialize safely
in one page session. A game is a single full-screen surface, not a website.

**`HelloWorld.Desktop/Program.cs`**

```csharp
using HelloWorld;
using Rayharbor.Desktop;

DesktopRunner.Run<HelloWorldGame>(new()
{
    Title = "Hello, Rayharbor!",
    Width = 1280,
    Height = 720,
    EnableAudio = true,
});
```

---

## 5. Runtime architecture

### 5.1 The contract

Keep it deliberately small. The game uses Raylib-cs directly for drawing, input and resources; the base only
owns the pieces that differ between desktop and browser.

```csharp
namespace Rayharbor;

public interface IGame : IDisposable
{
    void Initialize(GameContext context);
    void Update(float deltaTime);
    void Draw();
}

public abstract class GameBase : IGame
{
    protected GameContext Context { get; private set; } = null!;

    public void Initialize(GameContext context)
    {
        Context = context;
        OnInitialize();
    }

    public abstract void Update(float deltaTime);
    public abstract void Draw();

    protected abstract void OnInitialize();
    public virtual void OnResize(int width, int height) { }
    public virtual void Dispose() { }
}
```

`GameContext` provides the platform-specific services, but never wraps raylib rendering:

```csharp
public sealed class GameContext
{
    public required int Width { get; init; }        // logical/backing size
    public required int Height { get; init; }
    public required bool IsBrowser { get; init; }
    public required IRuntimeHost Runtime { get; init; }        // quit, fullscreen
    public required IMouseCapture MouseCapture { get; init; }  // pointer lock / cursor
    public required IGameLog Log { get; init; }
    public required GameDiagnostics Diagnostics { get; init; }
}

public interface IMouseCapture
{
    bool IsCaptured { get; }
    void Request();                    // web: must originate from a user gesture
    void Release();
    Vector2 ConsumeDelta();            // pixels since the last call
}

public interface IRuntimeHost
{
    bool IsBrowser { get; }
    bool IsFullscreen { get; }
    void RequestQuit();
    void ToggleFullscreen();
}
```

**Why no scene/asset/input abstractions?**

- Existing raylib games already use `LoadTexture`, `IsKeyDown`, `BeginDrawing`, etc. Wrapping them is a
  porting tax and a source of bugs.
- The only APIs that genuinely differ on the web are window lifecycle, cursor/pointer lock, fullscreen,
  audio unlock and file access. Those are exactly what `GameContext` covers.
- Scene management is game-specific. Games can bring their own `IScene`/state machine; the base should not
  invent one.

**Optional `GameDiagnostics`** (read-only): FPS, frame time, canvas/backing size, scale, asset mode. Updated
once per second by the base; safe for a debug overlay. Never a per-frame Blazor render.

### 5.2 Boot sequence

```
Blazor starts
  -> <RayharborCanvas> renders <canvas> + overlay ("Loading...")
  -> OnAfterRenderAsync(firstRender)
  -> import ./_content/Rayharbor.Web/rayharbor.js
  -> JS init(canvasId, handle, assemblyName):
       - bind runtime module canvas
       - set tabIndex, focus/contextmenu/keyboard listeners
       - ResizeObserver + visibilitychange + fullscreenchange
       - return DPR / CSS size
  -> assets:
       Bundled: already in VFS (loaded by the WASM runtime)
       Preload: fetch manifest -> write files to MEMFS -> progress to overlay
  -> if RequireUserGesture: show "Click to play"; wait for click
  -> InitWindow(logicalW, logicalH, title)
  -> if EnableAudio: InitAudioDevice()
  -> game.Initialize(context)
  -> JS start(handle): requestAnimationFrame loop
  -> hide overlay
```

Notes:

- `OnAfterRenderAsync` is used because the canvas must exist in the DOM before the module binds it. The
  Wolfrender `OnInitializedAsync` + `await` trick is fragile and is not repeated.
- `InitAudioDevice` must run from the click handler (synchronously, before any `await`) to satisfy browser
  autoplay policies. Keep asset fetching before the click, so the gesture is not lost.
- `InitWindow` does not need a gesture, but doing it together with audio keeps the sequence simple and lets
  the first frame appear immediately.
- The start overlay is optional (`RequireUserGesture=false` for silent games).

### 5.3 Frame loop

**Primary: JS `requestAnimationFrame` -> one synchronous `[JSExport]`.**

```js
function frame(now) {
    if (!running) return;
    raf = requestAnimationFrame(frame);

    const dt = Math.min((now - last) / 1000, 0.1); // clamp tab-switch spikes
    last = now;

    if (targetFps > 0) {
        acc += dt;
        if (acc < 1 / targetFps) return;
        acc = 0;
    }

    if (!exports.Rayharbor.Web.Interop.RayharborBridge.UpdateFrame(handle, dt)) {
        running = false;
        cancelAnimationFrame(raf);
    }
}
```

C# side:

```csharp
[JSExport]
private static bool UpdateFrame([JSMarshalAs<JSType.Any>] object handle, float deltaTime)
{
    if (handle is not RayharborCanvas canvas) return false;

    try
    {
        canvas.Tick(deltaTime);   // game.Update + game.Draw
        return true;
    }
    catch (Exception ex)
    {
        canvas.ReportCrash(ex);
        return false;
    }
}
```

**Why not `emscripten_set_main_loop`?** It works (Wolfrender exposes it), but:
- it requires an unmanaged function-pointer callback and careful cancellation;
- it bypasses the JS layer where pacing, visibility and error reporting live;
- the official Raylib-cs web example uses the rAF + `[JSExport]` pattern, so it is the best-supported path.

**Why not `async Task` per frame?** It creates a promise/allocation every frame and makes exceptions harder
to observe. The export is synchronous and returns `false` to stop the loop.

**Pacing:** default `TargetFps = 0` (rAF/vsync). Do **not** call `SetTargetFPS` in game code on the web:
raylib's limiter busy-waits in `EndDrawing` and can peg the main thread (documented by the Raylib-cs example).
The host paces in JS when a game explicitly asks for a cap. rAF caps at the display refresh rate.

**Delta time:** the host passes the rAF delta (clamped). `Raylib.GetFrameTime()` also works, but the host
value is stable and independent of `EndDrawing` internals. Document that `Update(dt)` is the source of truth.
On desktop, use `GetFrameTime()` clamped the same way.

### 5.4 Canvas sizing and mouse mapping

Two modes for v1:

| Mode | Backing store | CSS | Use when | Mouse |
|---|---|---|---|---|
| **Fit** (default) | fixed logical size (e.g. 1280x720) | letterboxed, aspect preserved, integer scaling optional | pixel-art, fixed-resolution games, ports with layout assumptions | `SetMouseScale(backing/css)` + `SetMouseOffset(0,0)` |
| **Fill** | CSS size (DPR 1) | fills viewport | responsive/fullscreen games, FPS, desktop-like | 1:1 |

Fit is the better default for "port any game": the game sees a stable resolution and does not need to handle
arbitrary window sizes. Fill is available for games that already handle `GetScreenWidth/Height`.

Implementation details:

- The canvas backing size is set by JS (`canvas.width/height`) **before** `InitWindow`, and on mode changes.
- In Fit mode, the JS resize handler only changes CSS size; C# only updates the mouse scale. No
  `SetWindowSize` per resize, no per-frame work.
- In Fill mode, JS sets `canvas.width = clientWidth` (and height) and notifies C# once; C# calls
  `SetWindowSize`, then `game.OnResize`.
- Mouse coordinates are CSS pixels. `SetMouseScale` maps them to the backing buffer. For pointer-lock
  deltas, the base multiplies `movementX/Y` by the same scale before handing them to the game.
- **HiDPI** is a separate future mode (`FillHiDpi`: backing = CSS * DPR, game must scale UI). Do not mix it
  into v1; it is a common source of blurry rendering and offset clicks.
- Resize notifications are coalesced: JS sets a pending size, the next `UpdateFrame` applies it. This avoids
  re-entrancy and Blazor renders during resize.

Fit-mode math (pure, unit-tested in `CanvasLayout`):

```
scale   = min(viewportW / logicalW, viewportH / logicalH)
cssW    = logicalW * scale
cssH    = logicalH * scale
mouse   = backing / css   // e.g. 1280 / 960 = 1.333
```

Integer scaling is an option (`floor(scale)`), useful for pixel art. Default is fractional fit.

### 5.5 Input and pointer lock

- The canvas gets `tabindex="0"` and is focused on pointer down. This is what makes Emscripten's GLFW
  keyboard events reach raylib.
- The base prevents default on keys that scroll the page (Space, arrows, Tab) while the game is running.
  It does **not** swallow browser-reserved shortcuts (Ctrl/Cmd+W, F5, etc.).
- `contextmenu` is prevented on the canvas only.
- **Pointer lock** is implemented in the base (`BrowserMouseCapture`):
  - `Request()` calls `canvas.requestPointerLock()`; on the web this must happen in a user gesture
    (typically a click on the canvas or a "Capture mouse" button).
  - `document.pointerlockchange` updates `IsCaptured`.
  - `mousemove` while locked accumulates `movementX/Y`; `ConsumeDelta()` returns and clears it.
  - Escape releases the lock (browser behavior); the game sees `IsCaptured == false`.
  - Desktop implementation: `DisableCursor()` / `EnableCursor()`, delta from `GetMouseDelta()`.
- raylib's `DisableCursor()` / `ConfigFlags` cursor modes are **not honored in the browser**; do not rely on
  them in ported code. Use `Context.MouseCapture`.
- Touch/gamepad: out of scope for v1. The base should not claim support it does not test.

### 5.6 Audio

- `InitAudioDevice()` is called by the host after a user gesture if `EnableAudio`.
- If initialization fails, the game still runs with `Context.AudioAvailable = false`; loading sounds should
  be guarded or allowed to fail.
- `CloseAudioDevice()` on teardown.
- Formats: WAV/OGG/MP3 (raylib/miniaudio). Prefer OGG for music and WAV for short effects. Everything is
  decoded from memory after preload; there is no HTTP streaming.
- `SetMasterVolume`/SFX volume are game concerns.
- Autoplay: without a gesture, the browser may create a suspended `AudioContext`. `RequireUserGesture=true`
  avoids this. Games with audio should keep it true.

### 5.7 Assets

The browser has no synchronous HTTP file access, and raylib's loaders are synchronous. Assets must be in the
Emscripten MEMFS before the game loads them. Two supported modes:

**Bundled (default, simplest)**

```xml
<WasmFilesToIncludeInFileSystem Include="$(GameAssetsDir)**\*"
                                TargetPath="assets\%(RecursiveDir)%(Filename)%(Extension)" />
```

The .NET WASM runtime loads these into the virtual filesystem at startup; the game uses `assets/...`
unchanged. Pros: zero host code, works offline after load, no fetch/progress code. Cons: everything loads
before the first frame; no per-level grouping; big games pay a long startup with only a generic spinner.

**Preload (for large games)**

- A manifest lists files (and optional groups).
- JS fetches each file, creates directories and writes to MEMFS, reporting progress to the Blazor overlay.
- Groups can be loaded later during a loading screen (`await Context.Assets.LoadGroupAsync("level2")`), but
  never in the middle of `Update` without a loading state.
- Pros: progress UI, selective loading, smaller initial download if groups are deferred. Cons: more moving
  parts and a manifest to generate.

**Decision:** implement Bundled for Hello World; add Preload in M3 before porting Wolfrender (5 MB of assets
and a hardcoded list today). The API should allow both from the start (`GameOptions.AssetMode`).

**Desktop:** the same `Assets/` folder is copied to `assets/` next to the executable; raylib's normal file
IO reads it. Keep the working directory at the output folder (as Wolfrender does).

**Case and paths:** always lowercase `assets/`; no leading `/`; no `..`; no absolute paths. Browsers and
Linux are case-sensitive.

### 5.8 Errors, loading and diagnostics

- **Boot errors** (module import, canvas missing, `InitWindow` failure, asset 404): show a Razor overlay
  with the message, the browser console hint and a "Reload" button. Never leave a black canvas.
- **Game-loop errors**: `UpdateFrame` catches everything, logs it, stops the loop, disposes the game and
  shows the overlay. This is the equivalent of Wolfrender's missing error boundary.
- **WebGL context loss**: listen for `webglcontextlost`, pause the loop and show "Graphics context lost -
  reload". Restoring a raylib GL context in place is not reliable; a reload is the honest fix.
- **Visibility**: `visibilitychange` pauses the loop and resets the clock; `dt` is clamped to 100 ms.
- **Diagnostics**: FPS, frame time, logical/backing/CSS size, scale, asset mode, .NET/raylib versions.
  A debug overlay (F3) updates at most 4x/second; it never causes a per-frame Blazor render.
- **Logging**: `IGameLog` writes to the browser console with a `[game]` prefix. Console output is the
  primary debugging tool; document how to use DevTools.

### 5.9 Desktop parity

`Rayharbor.Desktop` is intentionally tiny:

```csharp
public static class DesktopRunner
{
    public static void Run<T>(GameOptions options) where T : IGame, new()
    {
        SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        InitWindow(options.Width, options.Height, options.Title);
        SetExitKey(KeyboardKey.Null);          // let the game decide what Escape means
        if (options.EnableAudio) InitAudioDevice();

        var game = new T();
        var context = DesktopContext.Create(options);
        game.Initialize(context);

        while (!WindowShouldClose())
        {
            var dt = Math.Min(GetFrameTime(), 0.1f);
            game.Update(dt);
            game.Draw();
        }

        game.Dispose();
        if (options.EnableAudio) CloseAudioDevice();
        CloseWindow();
    }
}
```

- Poll `GetScreenWidth/Height` each frame and call `game.OnResize` when they change.
- `DesktopMouseCapture` uses `DisableCursor`/`EnableCursor` and `GetMouseDelta`.
- `DesktopRuntimeHost` maps `RequestQuit` to a flag checked by the loop and `ToggleFullscreen` to raylib's
  `ToggleFullscreen`.
- No DI requirement; `new()` keeps ported games simple. The web host uses DI for services; the game
  constructor stays parameterless unless a game explicitly needs injection.

**Value:** if the same `HelloWorldGame` runs on both hosts, the portability contract is real. This is the
cheapest architectural test we can have.

### 5.10 Browser constraints - the porting contract

These are the rules a ported game must respect. They belong in `docs/PORTING-GUIDE.md` and in the template's
README.

| Desktop code | Browser behavior | Required change |
|---|---|---|
| `while (!WindowShouldClose()) { Update; Draw; }` | A blocking loop freezes the page | Move the body into `Update`/`Draw`; host owns the loop |
| `InitWindow` / `CloseWindow` | Host owns the single window | Remove from game; use `GameOptions` |
| `SetTargetFPS` | raylib's limiter busy-waits on web | Use host `TargetFps`; do not call `SetTargetFPS` |
| `DisableCursor`, `ConfigFlags` | Not honored by the browser | Use `Context.MouseCapture` |
| `ToggleFullscreen` | Needs the DOM Fullscreen API + gesture | Use `Context.Runtime.ToggleFullscreen` |
| `Thread`, `Task.Run`, `Thread.Sleep` | Single-threaded runtime; no blocking | Remove; use delta time and async state machines |
| File reads from arbitrary paths | Only the MEMFS/VFS exists | Use `assets/...`, Bundled/Preload |
| File writes | Writes go to MEMFS and vanish on reload | Use browser storage/download via JS if persistence is needed |
| Native P/Invoke / `dlopen` | Only what is statically linked | Remove or replace with managed code |
| `System.Net.Http` | Works, subject to CORS | Prefer same-origin assets; configure CORS for APIs |
| File dialogs, window title/icon | Not available / ignored | Use Blazor UI; set `document.title` |
| Gamepad | Not guaranteed in the browser | Treat as unsupported in v1 |
| Reflection-heavy serialization | Trimming/AOT can break it | Prefer source-generated `System.Text.Json` |
| `Environment.Exit` | Kills the runtime | Use `Context.Runtime.RequestQuit` |
| Shaders | GLSL ES (`#version 100`/`300 es`) | Add `#if BROWSER` variants (see Raylib-cs web examples) |
| Screenshots | Works into MEMFS | Add a JS download step if needed |
| `DateTime.Now`, `Stopwatch` | Work | Fine |

---

## 6. Build, packaging and deployment

### 6.1 Native raylib: use Raylib-cs 8.1.0, do not vendor

The decisive finding: **Raylib-cs 8.1.0 (September 2026) supports `browser-wasm` officially.** The NuGet
package ships:

```
runtimes/browser-wasm/native/raylib.a
buildTransitive/Raylib-cs.targets
```

and the targets, when `RuntimeIdentifier == browser-wasm`:

- add `-sUSE_GLFW=3` to `EmccFlags`;
- add `NativeFileReference` for the shipped `raylib.a`;
- (for project references) stage the native library automatically.

That removes the need for a committed `raylib.a`, a custom `Raylib.props` and an ABI/version mismatch. It also
pins raylib to the version the bindings were generated against (raylib 6.0).

**Fallback:** if the M0 spike shows that 8.1.0 does not work with Blazor WASM, use the Wolfrender-proven
combination: Raylib-cs 7.0.2 + a committed Emscripten `raylib.a` (5.6-dev) + `Raylib.props`
(`WasmBuildNative`, `NativeFileReference`, `-sUSE_GLFW=3`, `-sALLOW_MEMORY_GROWTH=1`,
`-sFORCE_FILESYSTEM=1`, `-sTOTAL_MEMORY=67108864`). Do not maintain both paths; pick one after M0.

**Consequence for Wolfrender:** Wolfrender is on Raylib-cs 7.0.2/raylib 5.5-era APIs. Raylib-cs 8.x moved to
raylib 6.0, so porting it is not just a project move; it is an API upgrade. Keep that as a separate track
(M6) and do not let it block the base.

### 6.2 MSBuild properties

The framework package (`Rayharbor.Web/buildTransitive/Rayharbor.Web.props`) sets only what the *host* needs
and leaves app policy to the host:

```xml
<Project>
  <PropertyGroup Condition="'$(RuntimeIdentifier)' == 'browser-wasm'">
    <WasmBuildNative>true</WasmBuildNative>
    <WasmAllowUndefinedSymbols>true</WasmAllowUndefinedSymbols>
    <InvariantGlobalization>true</InvariantGlobalization>

    <!-- Overridable per game. -->
    <RayharborInitialMemory Condition="'$(RayharborInitialMemory)' == ''">64MB</RayharborInitialMemory>
    <EmccFlags>$(EmccFlags) -sALLOW_MEMORY_GROWTH=1 -sINITIAL_MEMORY=$(RayharborInitialMemory)</EmccFlags>
  </PropertyGroup>
</Project>
```

Host (`HelloWorld.Web.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <WasmGenerateAppBundle>true</WasmGenerateAppBundle>
    <SelfContained>true</SelfContained>

    <!-- Start without AOT; enable for Release after measuring (M2). -->
    <RunAOTCompilation Condition="'$(Configuration)' == 'Release'">true</RunAOTCompilation>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.*" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer"
                      Version="10.0.*" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\src\Rayharbor.Web\Rayharbor.Web.csproj" />
    <ProjectReference Include="..\HelloWorld.Game\HelloWorld.Game.csproj" />
  </ItemGroup>

  <Import Project="..\Assets.props" />
</Project>
```

Notes / things to verify in M0:

- `buildTransitive` import order and whether `EmccFlags` is appended cleanly to Raylib-cs's `-sUSE_GLFW=3`.
- `WasmFilesToIncludeInFileSystem` works under `Microsoft.NET.Sdk.BlazorWebAssembly` (the official example
  uses the plain WASM SDK).
- `SelfContained=true` is harmless for Blazor WASM.
- `RunAOTCompilation` is slow; measure Debug vs Release and only enable in Release if it pays off.
- `WasmEnableExceptionHandling` is not set initially. If exceptions from `[JSExport]` misbehave under AOT,
  enable it (Wolfrender does).
- Do **not** set `-sEXPORTED_RUNTIME_METHODS=ccall`; we do not call into raylib from JS.
- Do **not** duplicate `-sUSE_GLFW=3`; Raylib-cs adds it.

### 6.3 RCL packaging

`Rayharbor.Web` is a Razor Class Library (`Microsoft.NET.Sdk.Razor`), `IsPackable=true`,
`SupportedPlatform=browser`.

- Static web assets: `wwwroot/rayharbor.js` is served at
  `_content/Rayharbor.Web/rayharbor.js`; the component imports it with
  `JSHost.ImportAsync("rayharbor", "./_content/Rayharbor.Web/rayharbor.js")`.
  `./` resolves against the document base, so sub-path deployments work.
- Scoped CSS: `RayharborCanvas.razor.css` styles the container, canvas and overlays. No global CSS file the
  host must remember to link.
- The JS receives the **assembly name** from C# (`typeof(RayharborBridge).Assembly.GetName().Name`) instead
  of hardcoding it, and resolves both `Blazor.runtime` and `getDotnetRuntime(0)`.
- The canvas ID is generated per component; JS never assumes `"canvas"`.
- Package dependencies: `Raylib-cs [8.1.0, )`, `Microsoft.AspNetCore.Components.Web 10.0.x`.
- `buildTransitive` props ship in the package; they must resolve the native library path relative to the
  package root, not the repo.
- Include `README.md`, `LICENSE`, icon, `PackageTags` (`raylib`, `blazor`, `wasm`, `game`).

Pack layout:

```
Rayharbor.Web.0.1.0-preview.1.nupkg
├── lib/net10.0/Rayharbor.Web.dll
├── staticwebassets/...                 # raylib.js, scoped CSS
├── buildTransitive/Rayharbor.Web.props
├── README.md
└── LICENSE
```

### 6.4 Local development and publish

| Task | Command |
|---|---|
| Prereqs | `dotnet workload install wasm-tools` (currently **not installed** on this machine) |
| Run web | `dotnet run --project games/HelloWorld/HelloWorld.Web` |
| Run desktop | `dotnet run --project games/HelloWorld/HelloWorld.Desktop` |
| Publish | `dotnet publish games/HelloWorld/HelloWorld.Web -c Release -o artifacts/hello-world` |
| Serve published | `dotnet serve -d artifacts/hello-world/wwwroot` (or nginx; must serve `application/wasm`) |
| Test | `dotnet test` |

- `dotnet run` uses the Blazor DevServer. If Debug + native linking is slow or broken, use
  `dotnet publish -c Debug` and a static server; document the working path after M0.
- **Hot reload is disabled for the canvas host.** `dotnet watch` can restart managed code while the native
  raylib window is still initialized, which cannot work. Rebuild + refresh the page. The base should detect
  a second `InitWindow` and show a clear "reload the page" message instead of crashing.
- Published output is `artifacts/hello-world/wwwroot/`. This is what gets deployed.
- `<base href="./" />` in `index.html` makes sub-path hosting work. No router means no `404.html` fallback is
  strictly needed, but CI copies `index.html` to `404.html` anyway for future routes.
- Add `.nojekyll` for GitHub Pages.

### 6.5 CI and deployment

`ci.yml` (on push/PR):

1. `actions/checkout`
2. `actions/setup-dotnet` with `10.0.x`
3. `dotnet workload install wasm-tools`
4. `dotnet restore`
5. `dotnet build -c Release --no-restore`
6. `dotnet test -c Release --no-build`
7. `dotnet publish games/HelloWorld/HelloWorld.Web -c Release -o artifacts/hello-world`
8. Assert key files exist (`_framework/dotnet.wasm`, `_content/Rayharbor.Web/rayharbor.js`, the bundled
   assets).
9. Upload `artifacts/hello-world/wwwroot` as an artifact.

`deploy-hello-world.yml` (main/tags): deploy the artifact to GitHub Pages. Add `.nojekyll`, copy
`index.html` to `404.html`, optionally a `CNAME`.

Caching: `~/.nuget/packages` and the workload install are the main costs. Cache NuGet; do not over-engineer
workload caching until build times are known.

### 6.6 Version matrix

| Component | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.4xx | `global.json`, `rollForward: latestFeature` |
| Blazor WebAssembly | 10.0.x | standalone, no server project |
| Raylib-cs | 8.1.0 | raylib 6.0, official browser-wasm package |
| wasm-tools | match SDK | `dotnet workload install wasm-tools` |
| Emscripten | from wasm-tools | do not mix with a system EMSDK on `PATH` |
| xUnit | current | unit + architecture tests |
| Playwright | current | optional browser smoke test |

---

## 7. Hello World

Full spec and acceptance criteria: [HELLO-WORLD.md](HELLO-WORLD.md). Summary:

- **Goal:** prove the whole stack and the portability contract with the smallest interesting game.
- **Visual:** dark background, rotating 3D cube, "Hello, Rayharbor!" text, a small logo texture, mouse
  marker, FPS/resolution readout.
- **Input:** Space toggles rotation, click plays a short beep and spawns a fading ring, F toggles fullscreen,
  R resets. Mouse marker must track correctly at every scale.
- **Assets:** `logo.png`, `blip.ogg`, `ui.ttf` - enough to prove texture, audio and font loading.
- **Hosts:** the identical `HelloWorldGame` runs in `HelloWorld.Web` (Blazor) and `HelloWorld.Desktop`
  (native window).
- **Layout:** 1280x720 logical, Fit mode, letterboxed; resizing must never stretch the game or offset the
  mouse.
- **Size:** roughly 150-200 lines of game code. No engine abstractions.

Why this and not just a cleared screen: a clearing test proves linking, but not input, timing, assets, audio,
resize, mouse mapping or desktop parity. The cube demo exercises all of them for very little code.

---

## 8. Porting guide (draft)

Full draft: [PORTING-GUIDE.md](PORTING-GUIDE.md). The mechanical sequence:

1. **Extract** the game into a `net10.0` class library. Remove `Program.Main`, the `while` loop and any
   window/audio setup.
2. **Split `Main`:**
   - setup -> `OnInitialize` / `Initialize`
   - loop body -> `Update(dt)` + `Draw()`
   - cleanup -> `Dispose`
   - loop-spanning locals -> fields.
3. **Remove** `InitWindow`, `CloseWindow`, `SetTargetFPS`, `DisableCursor`, `ToggleFullscreen`,
   `ConfigFlags`, `Environment.Exit` from the game. Use `GameOptions`/`GameContext`.
4. **Assets:** move to `Assets/`, use `assets/...` relative paths, add `Assets.props`.
5. **Threads/timers:** remove `Thread.Sleep`/`Task.Run`; use delta time and async state machines.
6. **Shaders:** add a GLSL ES variant (`#version 100`/`300 es`) behind a `BROWSER` define or a runtime
   branch.
7. **Test desktop first** (fast, debuggable), then the web host.
8. **Profile** startup, memory and bundle size; choose Bundled vs Preload.
9. **Add the start overlay** for audio; verify pointer lock if the game needs it.
10. **Run** the compatibility checklist and the resize/mouse/DPR matrix.

Worked example for M3: **DoomFire-Raylib** (`~/Work/DoomFire-Raylib`) - small, shader-based, no server, no
editor. It is a better "arbitrary game" test than a second hello world.

Wolfrender is the large case (M6), not the first port. Known blockers:

- Raylib-cs 7.0.2 -> 8.1.0 / raylib 6.0 API changes.
- 5 MB of assets and a hardcoded preload list.
- Custom pointer lock, frame pacing, debug console and `Game.Engine.*`/`Game.Core.*` namespaces.
- Desktop ImGui editor and `rlImGui` (desktop only; should not be referenced by the web host).
- Highscore server (out of scope for a static client).
- Custom shaders and per-face textures (GLSL ES variants).

---

## 9. Roadmap

| Milestone | Deliverable | Exit criteria |
|---|---|---|
| **M0 - Spike** (0.5-1 day) | Throwaway Blazor app using Raylib-cs 8.1.0: canvas, `InitWindow`, clear + text, rAF `[JSExport]` loop, bundled asset, click beep, resize/mouse scale. | Runs in Chrome and Firefox; publish + static serve works; findings written to `docs/spikes/0001-*.md`; go/no-go on 8.1.0. |
| **M1 - Skeleton** | Solution, 3 framework projects, `IGame`/`GameContext`, host boots to a Razor loading screen, CI build. | `dotnet build` green; dependency rules documented; no game logic. |
| **M2 - Hello World** | Base canvas component, JS bridge, desktop runner, Hello World game + hosts, bundling, audio, resize, error/loading overlays. | All Hello World acceptance criteria pass; same game on desktop and web. |
| **M3 - Real port** | Preload/manifest assets, pointer lock/mouse look, diagnostics, DoomFire port. | DoomFire runs on web and desktop; porting guide validated; list of friction points. |
| **M4 - Template & packages** | `dotnet new raylib-game`, packable `Rayharbor.Core/Web/Desktop`, local feed, docs. | A new game can be created and run from the template without touching the base source. |
| **M5 - CI & deploy** | GitHub Actions publish, Pages deploy, Playwright smoke test, size/time report. | Green CI on a clean runner; deployed demo URL; smoke test catches a blank canvas. |
| **M6 - Wolfrender port** (separate track) | Port Wolfrender onto Rayharbor. | Game playable in the browser; no Wolfrender-specific code in the base. |

M0 is the decision gate. Do not build M1-M2 until the 8.1.0-in-Blazor question is answered.

---

## 10. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Raylib-cs 8.1.0 browser-wasm does not work with Blazor (only with the plain WASM SDK) | Medium | High | M0 spike first; fallback to 7.0.2 + Wolfrender's proven `raylib.a` + props. |
| raylib 6.0 API changes make porting existing games expensive | High for Wolfrender | Medium | Separate M6; base stays at 8.1.0; document the diff; don't port during base work. |
| `wasm-tools` / SDK / Emscripten mismatch (`wasm-opt` errors) | Medium | Medium | Pin SDK in `global.json`; `dotnet workload update`; official example's `-O0` link fallback; CI installs the workload fresh. |
| AOT build time/size is unacceptable | Medium | Medium | Start interpreter/Debug; enable AOT only in Release after measuring; record numbers. |
| Asset memory blow-up (MEMFS + decoded textures/audio) | Medium | Medium | Bundled only for small games; Preload groups for large ones; `ALLOW_MEMORY_GROWTH`; document limits. |
| Blazor render and game loop compete for the main thread | Low-Medium | Medium | No per-frame Blazor; overlays only; measure frame time with the diagnostics overlay. |
| Pointer lock/audio/fullscreen gestures behave differently across browsers | Medium | Medium | Start overlay; test Chrome/Firefox/Safari; degrade gracefully; document unsupported features. |
| HiDPI/mouse mapping bugs | Medium | Medium | Fit mode + `SetMouseScale`; unit-test layout math; manual DPR/zoom matrix. |
| Hot reload / remount double-inits raylib | High (if using `dotnet watch`) | Low | Disable hot reload for the canvas; guard against a second init; show "reload" message. |
| GitHub Pages sub-path breaks absolute URLs | Low | Medium | `<base href="./">`; relative JS/asset URLs; test under `/repo/`. |
| Trimming/AOT breaks reflection-based game code | Medium | Medium | Prefer source-generated JSON; document trim warnings; `TrimmerRootAssembly` escape hatch per game. |
| Base creeps into an engine | High over time | High | Explicit non-goals; keep `Rayharbor.Core` tiny; reject PRs that add scene/physics/editor features. |
| Licence/attribution mistake | Low | Medium | MIT for our code; keep raylib/raylib-cs notices; no Wolfrender code copied. |

---

## 11. Decisions needed from review

Please answer these before M1. Defaults are in **bold**.

1. **Name:** **Rayharbor** / Slipway / Rayport / Porthole / Rayforge / other?
2. **Structure:** **A (layered monorepo)** / B (base + game app) / C (NuGet-first) / D (single project)?
3. **Raylib baseline:** **Raylib-cs 8.1.0 (raylib 6.0, official WASM)** / stay on 7.0.2 for Wolfrender
   compatibility?
4. **Desktop parity now?** **Yes, thin `Rayharbor.Desktop`** / later / no?
5. **Default canvas mode:** **Fit (fixed logical resolution)** / Fill (responsive) / both with Fit default?
6. **Default asset mode:** **Bundled** / Preload / both (Bundled first)?
7. **AOT:** interpreter in Debug, **AOT in Release (measure first)** / AOT everywhere / never?
8. **Repo layout:** `src/` + `games/` + `tests/` + `templates/` as in 4.4 / flatter / different names?
9. **Licence:** **MIT** / other?
10. **Package IDs/remote:** reserve `Rayharbor.*` on NuGet? GitHub org/user for the remote?
11. **Multi-game host:** **no** (one host per game; gallery later) / yes?
12. **First real port:** **DoomFire-Raylib** / another small game / none (Hello World only)?
13. **Wolfrender as M6:** **yes, separate track** / not planned / do it earlier?
14. **Target browsers:** Chrome + Firefox first, Safari best-effort? Mobile out of scope?

Also worth deciding now: should the docs use British or American spelling, and should the repo be public from
the start? (The current docs mix "licence"/"license"; pick one.)

---

## 12. References

- Raylib-cs 8.1.0: <https://github.com/raylib-cs/raylib-cs/releases/tag/v8.1.0>
  - browser-wasm support, `runtimes/browser-wasm/native/raylib.a`, `buildTransitive/Raylib-cs.targets`
  - web example: `Examples/Web/{README.md,Host.cs,main.js,index.html,scaleUtils.js}`
- Raylib 6.0: <https://github.com/raysan5/raylib/releases/tag/6.0>
- Blazor WebAssembly native dependencies:
  <https://learn.microsoft.com/aspnet/core/blazor/webassembly-native-dependencies>
- Blazor.Raylib (MIT): <https://github.com/trucidare/Blazor.Raylib>
- DotnetRaylibWasm: <https://github.com/stanoddly/DotnetRaylibWasm>
- Existing project: `~/Work/Wolfrender.Blazor.Raylib/OVERVIEW.md`, `Components/Raylib.razor.cs`,
  `wwwroot/js/raylib.js`, `Raylib.props`

---

## 13. Review checklist (short version)

- [ ] Name approved (or shortlist narrowed).
- [ ] Structure A/B/C chosen.
- [ ] Raylib-cs 8.1.0 accepted as the baseline (with 7.0.2 fallback).
- [ ] Desktop host in scope.
- [ ] Canvas mode + asset mode defaults agreed.
- [ ] AOT policy agreed.
- [ ] Licence + package IDs decided.
- [ ] M0 spike approved as the next action.

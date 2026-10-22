# Rayforge - Architecture & Implementation Plan

> Status: **M0-M4 done.** Framework + Hello World + DoomFire port on Raylib-cs 8.1.0; packed packages and
> `dotnet new rayforge-game` consume them from a local feed. Next: M5 CI/deploy.
> Date: 2026-10-21
> Repository: `~/Work/Rayforge` -> `kenwi/Rayforge` (public, MIT)
> Milestone tracker: [MILESTONES.md](MILESTONES.md)

---

## 0. Decisions at a glance

| # | Question | Decision |
|---|---|---|
| 1 | Name | **Rayforge**. Alternatives kept in section 3. |
| 2 | Repository shape | **Alternative A - layered monorepo**: `src/` framework + `games/<Game>/` with `.Game`, `.Web`, `.Desktop`. |
| 3 | Base + game separation | The game is a **plain `net10.0` class library**; the Blazor app is a **thin host**. The base is never copied into a game. |
| 4 | Raylib baseline | **Raylib-cs 8.1.0** (bindings + native raylib 6.0 + `runtimes/browser-wasm/native/raylib.a` from the package). No vendored binary, no custom props file. Settled by spike 0002 (M0b). |
| 5 | Who owns the window/loop | The **host** owns `InitWindow` / `InitAudioDevice` / the frame loop. The game implements `IGame` (`Initialize`, `Update`, `Draw`, `OnResize`, `Dispose`). |
| 6 | Frame loop | `requestAnimationFrame` in JS calls **one synchronous `[JSExport]`** per frame. No blocking `while` loop, no per-frame `async Task`, no per-frame C#->JS calls. |
| 7 | Canvas sizing | **Both modes**: **Fit** (fixed logical resolution, CSS letterboxing, `SetMouseScale`) and **Fill** (backing store = CSS pixels, responsive). Fit is the default. HiDPI is a later mode. |
| 8 | Assets | **Bundled** - every asset is fetched at boot and written into MEMFS before the first frame. Preload groups (manifest + progress) are added when a port needs them. |
| 9 | Desktop parity | **In scope from day one**, via a thin `Rayforge.Desktop` runner. It is the cheapest proof that the game code is portable. |
| 10 | Hello World | Rotating 3D cube + 2D text + texture + click sound + mouse marker + resize + fullscreen. Same game on web and desktop. |
| 11 | AOT | **Not in v1.** Measured in M5, enabled later only if it pays off. |
| 12 | License | **MIT** ([LICENSE](../LICENSE)). |
| 13 | First real port | **DoomFire-Raylib** (`~/Work/DoomFire-Raylib`), M3. |
| 14 | Wolfrender | Ported last, M6, on a separate track. |
| 15 | Out of scope | Engine/ECS, editor, ImGui, level format, server, networking, hot reload, mobile controls, multiple games per page. |

**M0 completed 2026-10-21: GO.** The stack renders in a fresh Blazor app (3D + 2D + texture + audio + letterboxed
mouse mapping, on the DevServer and from a static publish). Evidence, measurements and the four plan corrections:
[spikes/0001-raylib-cs-blazor-wasm.md](spikes/0001-raylib-cs-blazor-wasm.md).

**M0b completed 2026-10-21: GO.** Raylib-cs 8.1.0 replaces the vendored `raylib.a` and `Raylib.props` with no
behaviour change, zero undefined symbols, and a 2.7 % smaller payload:
[spikes/0002-raylib-cs-8.1.0.md](spikes/0002-raylib-cs-8.1.0.md). The upgrade workflow is
[UPGRADING-RAYLIB.md](UPGRADING-RAYLIB.md).


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

**Conclusion:** keep the technique, drop the coupling. Rayforge is a rewrite of the bridge around an explicit
game contract, not a refactor of Wolfrender.

### 1.4 Why not fork the existing base projects?

- **`trucidare/Blazor.Raylib`** is a nine-file demo, not a framework: it has no game contract, hardcodes the
  canvas ID and JS path, and does not ship a `raylib.a`; the consumer must import `Raylib.props` and supply
  the native library. It was a useful starting point for Wolfrender, but extending it means carrying the same
  limitations.
- **`DotnetRaylibWasm`** is an archived .NET 7 prototype for raylib 4.x. It is a great reference for the
  Emscripten/MSBuild tricks, not a maintained base.
- **`Raylib-cs` 8.1.0** ships official `browser-wasm` support (`raylib.a` + `buildTransitive` targets), which
  removes the main reason to carry a custom props file. **We adopt it** (spike 0002): the base takes the
  native archive, the `-sUSE_GLFW=3` flag and the desktop RID natives from the package.

The official Raylib-cs web example is still the reference for the native/JS loop; Rayforge adds the Blazor
host, game contract, desktop parity, overlays and packaging around the same technique.

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
user-driven overlay changes. If the UI framework ever becomes a problem, `Rayforge.Core` + the JS bridge
can be reused from a plain WASM host.

---

## 3. Name

### 3.1 Chosen: **Rayforge**

- "Ray" ties it to raylib; a forge is where a game is re-worked for another platform. It also matches the
  spirit of the project (re-forging an existing game for the browser).
- Reads well as a namespace and package prefix: `Rayforge.Core`, `Rayforge.Web`, `Rayforge.Desktop`.
- Repository: `kenwi/Rayforge` (public). NuGet IDs are placeholders until M4; check availability before packing.
- Tagline: *"Forge your Raylib game for the browser."*

### 3.2 Alternatives considered

| Name | Meaning / why | Risk / why not |
|---|---|---|
| **Rayforge** | **Chosen.** Raylib + forge: re-work a game for the web. | Implies an engine/editor; close to Wolfrender's branding. |
| **Rayharbor** | Raylib + harbor (a port). | Slightly abstract; replaced by Rayforge. |
| **Slipway** | A ramp for launching boats - launching games into the web. Short. | 136 GitHub repos; generic outside the metaphor. |
| **Rayport** | Most literal: raylib + port. | `atarigo/rayport` already packages Python raylib games for the web; confusing collision. |
| **Porthole** | A window into the game; friendly. | Sounds like a UI/widget library, not a runtime. |
| **Raylib.Web** | Maximum clarity and discoverability. | No brand; collides conceptually with `Blazor.Raylib`; "Blazor" is a Microsoft trademark best kept out of package IDs. |
| **Dockyard** | Where ships are fitted out. | Generic; no raylib signal. |

### 3.3 Rename strategy

The name is fixed before any project or namespace exists, so this is now a non-issue. Only documentation and the
repository folder ever carried the old name. Package IDs stay placeholders until M4.


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
is used from a non-Blazor host or raylib-cs changes; `Rayforge.Web` will still be right.

### 4.2 Structure alternatives

#### Alternative A - Layered monorepo (recommended)

```
Rayforge/
├── src/Rayforge.Core/          # portable contracts, no Blazor
├── src/Rayforge.Web/           # Blazor RCL: canvas, JS bridge, browser services
├── src/Rayforge.Desktop/       # optional desktop runner
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
Rayforge/
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
├── Directory.Packages.props     # Rayforge.Web 0.x
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

### 4.3 Decision

**Alternative A**, evolving into Alternative C at M4.

- During development, `ProjectReference` gives a fast inner loop and lets us debug the bridge.
- All three framework projects are `IsPackable=true` from day one; at M4 we pack them and the template
  consumes the package. No code moves.
- Alternative B was rejected: it cannot express desktop parity without Wolfrender's `Compile Include` trick,
  which is exactly the coupling this project exists to remove.
- Avoid D entirely for this project; it is the Wolfrender trap.

### 4.4 Tree (M1 creates everything below `spike/`)

```
Rayforge/
├── Rayforge.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
├── .gitignore
├── LICENSE                          # MIT
├── README.md
├── docs/
│   ├── PLAN.md
│   ├── MILESTONES.md
│   ├── HELLO-WORLD.md
│   ├── PORTING-GUIDE.md
│   └── spikes/
│       ├── README.md
│       └── 0001-raylib-cs-blazor-wasm.md    # M0 result
├── spike/m0-raylib-blazor/          # throwaway M0 probe, kept as evidence
├── src/
│   ├── Rayforge.Core/
│   │   ├── IGame.cs
│   │   ├── GameBase.cs
│   │   ├── GameContext.cs
│   │   ├── GameOptions.cs
│   │   ├── Diagnostics.cs
│   │   ├── IMouseCapture.cs
│   │   ├── IRuntimeHost.cs
│   │   ├── IGameLog.cs
│   │   └── Rayforge.Core.csproj            net10.0
│   ├── Rayforge.Desktop/
│   │   ├── DesktopRunner.cs
│   │   ├── DesktopMouseCapture.cs
│   │   ├── DesktopRuntimeHost.cs
│   │   └── Rayforge.Desktop.csproj         net10.0
│   └── Rayforge.Web/
│       ├── Components/
│       │   ├── RayforgeCanvas.razor
│       │   ├── RayforgeCanvas.razor.cs
│       │   └── RayforgeCanvas.razor.css
│       ├── Interop/RayforgeBridge.cs
│       ├── BrowserMouseCapture.cs
│       ├── BrowserRuntimeHost.cs
│       ├── CanvasLayout.cs                  # pure, unit-testable
│       ├── ServiceCollectionExtensions.cs
│       ├── wwwroot/rayforge.js
│       ├── buildTransitive/Rayforge.Web.targets
│       └── Rayforge.Web.csproj             net10.0 RCL
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
│   ├── Rayforge.Tests/                     # unit + architecture tests
│   └── HelloWorld.BrowserTests/             # Playwright smoke (M5)
├── templates/rayforge-game/                # M4
└── .github/workflows/
    ├── ci.yml
    └── deploy-hello-world.yml               # M5
```

### 4.5 Project responsibilities and dependency rules

```
HelloWorld.Game  ->  Rayforge.Core, Raylib-cs
HelloWorld.Web   ->  HelloWorld.Game, Rayforge.Web
HelloWorld.Desktop -> HelloWorld.Game, Rayforge.Desktop

Rayforge.Core     ->  Raylib-cs
Rayforge.Web      ->  Rayforge.Core, Raylib-cs, Microsoft.AspNetCore.Components.Web
Rayforge.Desktop  ->  Rayforge.Core, Raylib-cs
```

Rules, enforced by a unit test inspecting referenced assemblies:

- `Rayforge.Core` must not reference Blazor, JavaScript interop or WASM-specific APIs.
- `<Game>.Game` must not reference `Rayforge.Web`, `Rayforge.Desktop`, `Microsoft.AspNetCore.*` or
  `System.Runtime.InteropServices.JavaScript`.
- Hosts contain **no game rules**: config, asset wiring, DI, nothing else.
- The base contains **no game knowledge**: no namespaces, asset names, scene types or key bindings from a
  specific game.
- One raylib context per page. The base guards against a second `RayforgeCanvas` starting.

### 4.6 Where assets live

`games/<Name>/Assets/` is the single source of truth for both hosts. A tiny `Assets.props` imported by both
host projects maps the same files into each output. Each host sets `RayforgeHost` (`Web` or `Desktop`) and the
web host optionally sets `RayforgeAssetMode` (`Bundled` default, or `Preload`).

```xml
<Project>
  <PropertyGroup>
    <GameAssetsDir>$(MSBuildThisFileDirectory)Assets\</GameAssetsDir>
    <RayforgeAssetMode Condition="'$(RayforgeAssetMode)' == ''">Bundled</RayforgeAssetMode>
  </PropertyGroup>

  <!-- Desktop: assets next to the executable, read by raylib's normal file IO. -->
  <ItemGroup Condition="'$(RayforgeHost)' == 'Desktop'">
    <Content Include="$(GameAssetsDir)**\*"
             Link="assets\%(RecursiveDir)%(Filename)%(Extension)"
             CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <!-- Web (both asset modes): files must physically exist under wwwroot/assets before
       the static web asset manifest is resolved. The copy target is in the framework
       props; the generated folder is gitignored. -->
  <ItemGroup Condition="'$(RayforgeHost)' == 'Web'">
    <GameAsset Include="$(GameAssetsDir)**\*" />
  </ItemGroup>

  <Target Name="SyncGameAssetsIntoWwwroot"
          BeforeTargets="ResolveStaticWebAssetsInputs"
          Condition="'$(RayforgeHost)' == 'Web'">
    <Copy SourceFiles="@(GameAsset)"
          DestinationFiles="@(GameAsset->'$(MSBuildProjectDirectory)\wwwroot\assets\%(RecursiveDir)%(Filename)%(Extension)')"
          SkipUnchangedFiles="true" />
    <ItemGroup>
      <Content Include="wwwroot\assets\**\*" Exclude="@(Content)" />
    </ItemGroup>
  </Target>
</Project>
```

The M0 spike proved the two modes differ only in the fetch list and the progress UI, not in how files reach
MEMFS (see 5.7 and spike 0001, 4.1). `RayforgeAssetMode` therefore selects the list/manifest the host is given,
not the transport.

`RayforgeHost` is set in the host `.csproj` (`<RayforgeHost>Web</RayforgeHost>` /
`<RayforgeHost>Desktop</RayforgeHost>`). Without it, the imported props cannot tell the hosts apart and
assets get copied twice or into the wrong place.

Use lowercase `assets/` in code on every platform. On Linux, path case matters. The game must only use
relative paths (`assets/logo.png`), never a leading slash or an absolute path.

### 4.7 What each host looks like

**`HelloWorld.Web/Program.cs`**

```csharp
using HelloWorld;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Rayforge;
using Rayforge.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddRayforge<HelloWorldGame>(options =>
{
    options.Title = "Hello, Rayforge!";
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
<RayforgeCanvas />
```

No router is deliberate: navigation would dispose/recreate the canvas and raylib cannot re-initialize safely
in one page session. A game is a single full-screen surface, not a website.

**`HelloWorld.Desktop/Program.cs`**

```csharp
using HelloWorld;
using Rayforge.Desktop;

DesktopRunner.Run<HelloWorldGame>(new()
{
    Title = "Hello, Rayforge!",
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
namespace Rayforge;

public interface IGame : IDisposable
{
    void Initialize(GameContext context);
    void Update(float deltaTime);
    void Draw();
    void OnResize(int width, int height);
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

`GameContext` provides the platform-specific services, but never wraps raylib rendering. `Width`/`Height` are
the logical size at initialization; in Fill mode read `GetScreenWidth/Height` (or `OnResize`) for live
dimensions.

```csharp
public sealed class GameContext
{
    public required int Width { get; init; }        // logical/backing size
    public required int Height { get; init; }
    public required bool IsBrowser { get; init; }
    public required bool AudioAvailable { get; init; }
    public required IRuntimeHost Runtime { get; init; }        // quit, fullscreen
    public required IMouseCapture MouseCapture { get; init; }  // pointer lock / cursor
    public required IGameLog Log { get; init; }
    public required GameDiagnostics Diagnostics { get; init; }
}

public enum CanvasScaleMode { Fit, Fill }
public enum AssetMode { Bundled, Preload }

public sealed class GameOptions
{
    public string Title { get; set; } = "Rayforge Game";
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public CanvasScaleMode ScaleMode { get; set; } = CanvasScaleMode.Fit;
    public AssetMode AssetMode { get; set; } = AssetMode.Bundled;
    public bool EnableAudio { get; set; } = true;
    public bool RequireUserGesture { get; set; } = true;
    public int TargetFps { get; set; }             // 0 = rAF/vsync

    // Applied before InitWindow on desktop. Browser support varies; do not rely on it for
    // cursor/fullscreen behavior.
    public ConfigFlags ConfigFlags { get; set; } =
        ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint;
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

**Responsibility split**

| The base owns | The game owns |
|---|---|
| Canvas creation, module import, `InitWindow` / `CloseWindow` | Game loop body (`Update` / `Draw`) |
| Frame scheduling, delta clamping, pause on hidden tab | Drawing (`BeginDrawing` / `EndDrawing`, camera, shaders) |
| `InitAudioDevice` / `CloseAudioDevice` and the start gesture | Loading/playing sounds and music |
| Canvas sizing, letterboxing, mouse scale/offset | Layout inside the logical resolution |
| Pointer lock, cursor visibility, fullscreen | What mouse/key input means |
| Asset bundling/preloading into the MEMFS | Which assets exist and when they are used |
| Loading/error/crash overlays | Game UI/HUD and menus |
| Diagnostics counters | Save data, settings, progression |
| Teardown order and the "one raylib context" guard | Scene/level management |

### 5.2 Boot sequence

```
Blazor starts
  -> <RayforgeCanvas> renders <canvas> + overlay ("Loading...")
  -> OnAfterRenderAsync(firstRender)
  -> import an absolute URL for _content/Rayforge.Web/rayforge.js
  -> JS init(canvasId, handle, assemblyName):
       - bind runtime module canvas
       - set tabIndex, focus/contextmenu/keyboard listeners
       - ResizeObserver + visibilitychange + fullscreenchange
       - return DPR / CSS size
  -> assets:
       Bundled: fetch the build-generated list -> write every file to MEMFS
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

    if (!exports.Rayforge.Web.Interop.RayforgeBridge.UpdateFrame(handle, dt)) {
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
    if (handle is not RayforgeCanvas canvas) return false;

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
Emscripten MEMFS before the game loads them.

**The M0 spike invalidated the original "the runtime pre-populates the VFS" assumption.**
`WasmFilesToIncludeInFileSystem` is not implemented by the Blazor SDK: nothing is written to MEMFS and
`LoadTexture("assets/logo.png")` fails. Every web asset therefore goes through fetch -> `FS.writeFile`
(spike 0001, 4.1). The two modes differ only in *when* and *how the list is produced*:

**Bundled (default)**

- The build emits the list of files under `Assets/` (generated at compile time, not hand-maintained).
- At boot, before the first frame, JS fetches every file, creates the directories and writes it to MEMFS.
- The game uses `assets/...` unchanged.
- Pros: one code path, no manifest to keep in sync, no partial game state. Cons: everything loads before the
  first frame; big games pay a long startup with only a generic spinner.

```js
for (const path of assets) {
    const res = await fetch(new URL(path, document.baseURI));
    const data = new Uint8Array(await res.arrayBuffer());
    FS.mkdir("assets");                 // one mkdir per path segment
    FS.writeFile(path, data);
}
```

**Preload groups (for large games, M3)**

- A generated manifest lists files and optional groups.
- JS fetches each file, reporting progress to the Blazor overlay.
- Groups can be loaded later during a loading screen (`await Context.Assets.LoadGroupAsync("level2")`), but
  never in the middle of `Update` without a loading state.
- Pros: progress UI, selective loading, smaller initial download if groups are deferred. Cons: more moving
  parts.

**Decision:** Bundled for Hello World and DoomFire (M3; shaders are tiny). Add Preload groups before
Wolfrender (M6, ~5 MB of assets). `GameOptions.AssetMode` exists so the API does not change.

**Serving the files:** the shared `Assets/` folder must physically exist under the web host's `wwwroot/assets/`
before the static web asset manifest is resolved, otherwise the DevServer answers `200` with an empty body.
Use the copy target from spike 0001, 4.2 (in the framework props, gitignore the generated folder). Do not
combine it with a `Content ... Link` item: publish then fails with duplicate asset paths.

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

`Rayforge.Desktop` is intentionally tiny:

```csharp
public static class DesktopRunner
{
    public static void Run<T>(GameOptions options) where T : IGame, new()
    {
        SetConfigFlags(options.ConfigFlags);
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
| Shaders | GLSL ES (`#version 100` on Raylib-cs 8.1.0 web; desktop 330) | Select the folder at runtime (`Context.IsBrowser`); avoid `#if BROWSER`. Prefer `.txt` suffixes for DevServer MIME. See spike 0003. |
| Screenshots | Works into MEMFS | Add a JS download step if needed |
| `DateTime.Now`, `Stopwatch` | Work | Fine |

---

## 6. Build, packaging and deployment

### 6.1 Native raylib: Raylib-cs 8.1.0, native from the package

**Decision:** take everything from the NuGet package. There is no native binary in the repository.

| Piece | Version | Where it lives |
|---|---|---|
| Raylib-cs | 8.1.0 | `<PackageReference>` in `Rayforge.Core` (flows to everything) |
| `raylib.a` (web) | raylib 6.0, Emscripten/GLFW, 1.36 MB | the package: `runtimes/browser-wasm/native/raylib.a` |
| Desktop natives | raylib 6.0 | the package: `linux-x64`, `osx-x64`, `osx-arm64`, `win-x64`, `win-x86` |
| Link flags | `-sUSE_GLFW=3` only | the package: `buildTransitive/Raylib-cs.targets` |
| Our targets | `WasmBuildNative`, `EmccLinkOptimizationFlag=-O0`, `InvariantGlobalization` | `buildTransitive/Rayforge.Web.targets` |

The package's `buildTransitive` targets add `-sUSE_GLFW=3` and the `NativeFileReference` when
`RuntimeIdentifier == browser-wasm`, so the link gets exactly one archive and one flag (verified in
`emcc-link.rsp`, spike 0002, 4.1). Its archive is built from the same raylib release as the bindings, so the
three undefined symbols of the old pairing (`DrawLineBezierCubic`, `DrawLineBezierQuad`, `GetSoundsPlaying`)
are gone. The probe builds with **no** `WasmAllowUndefinedSymbols` and no warnings: a future mismatch becomes a
build error instead of a runtime trap. Keep it that way.

Consequences:

- **Do not** commit a `raylib.a`, `libraylib.so` or `raylib.dll`, and do not add a second `NativeFileReference`.
- **Do not** set `RuntimeIdentifier=browser-wasm` on `Rayforge.Core` or a game library; only the web host
  needs it, and a game library that sets it drags the native reference into other builds.
- The custom Emscripten flags from the old baseline are gone: `ALLOW_MEMORY_GROWTH` is on by default (heap
  32 MB -> 70.8 MB on a 40 MB allocation), `FORCE_FILESYSTEM` is unnecessary (MEMFS exists), `TOTAL_MEMORY` is
  obsolete, and `-DPLATFORM_WEB` is unnecessary because games branch on `Context.IsBrowser`, not on a define.

Upgrades follow [UPGRADING-RAYLIB.md](UPGRADING-RAYLIB.md); the spike record is
[spike 0002](spikes/0002-raylib-cs-8.1.0.md).


### 6.2 MSBuild properties

The framework package (`Rayforge.Web/buildTransitive/Rayforge.Web.targets`) sets only what the *host* needs:

```xml
<Project>
  <PropertyGroup Condition="'$(RuntimeIdentifier)' == 'browser-wasm'">
    <WasmBuildNative>true</WasmBuildNative>
    <WasmEnableExceptionHandling>true</WasmEnableExceptionHandling>
    <InvariantGlobalization>true</InvariantGlobalization>

    <!-- Temporary: -O2 makes the native link run wasm-opt with a flag Binaryen 117
         does not know (spike 0001, section 4.3; still true on 8.1.0, spike 0002,
         section 4.4). Revisit in M5. -->
    <EmccLinkOptimizationFlag>-O0</EmccLinkOptimizationFlag>
  </PropertyGroup>
</Project>
```

No `NativeFileReference`, no `EmccFlags`, no `WasmAllowUndefinedSymbols`: Raylib-cs 8.1.0's own
`buildTransitive` targets add the archive and `-sUSE_GLFW=3`, and a clean link is the ABI gate.

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
    <RayforgeHost>Web</RayforgeHost>
    <RayforgeAssetMode>Bundled</RayforgeAssetMode>

    <!-- AOT is opt-in and not part of v1; measured in M5.
    <RunAOTCompilation>true</RunAOTCompilation>
    -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.*" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer"
                      Version="10.0.*" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\src\Rayforge.Web\Rayforge.Web.csproj" />
    <ProjectReference Include="..\HelloWorld.Game\HelloWorld.Game.csproj" />
  </ItemGroup>

  <Import Project="..\Assets.props" />
</Project>
```

Notes, all confirmed or corrected by the M0 spike:

- The package's link flags (`-sUSE_GLFW=3`) and its `NativeFileReference` flow through `buildTransitive`; the
  framework props deliberately add **no** Emscripten flags (spike 0002, 4.1). Verify with
  `grep -c raylib.a obj/Release/net10.0/wasm/for-publish/emcc-link.rsp` after any package change.
- **`WasmFilesToIncludeInFileSystem` does not work with the Blazor SDK.** Assets are fetched at runtime and
  written into MEMFS instead (spike 0001, 4.1). `Assets.props` must therefore also emit the build-time asset
  list the host hands to `Rayforge.Web`.
- **Assets outside `wwwroot` need the copy target** from spike 0001, 4.2 (`Content` + `Link` serves 0 bytes on
  the DevServer). The target belongs in the framework props, not in each game.
- `SelfContained=true` is harmless for Blazor WASM.
- `RunAOTCompilation` is slow; keep it off until M5 has numbers.
- Do **not** set `-sEXPORTED_RUNTIME_METHODS=ccall`; nothing calls into raylib from JS.
- `-sUSE_GLFW=3` is provided by the Raylib-cs package, once. Do not add it again.


### 6.3 RCL packaging

`Rayforge.Web` is a Razor Class Library (`Microsoft.NET.Sdk.Razor`), `IsPackable=true`, with
`<SupportedPlatform Include="browser" />`.

- Static web assets: `wwwroot/rayforge.js` is served at
  `_content/Rayforge.Web/rayforge.js`. Build the URL from `NavigationManager.BaseUri` **and** pass an absolute
  URL to `JSHost.ImportAsync`: relative module URLs resolve against `_framework/`, not the document base
  (spike 0001, 4.4).

  ```csharp
  var url = new Uri(new Uri(Navigation.BaseUri), "_content/Rayforge.Web/rayforge.js").AbsoluteUri;
  await JSHost.ImportAsync("rayforge", url);
  ```

- Scoped CSS: `RayforgeCanvas.razor.css` styles the container, canvas and overlays. No global CSS file the
  host must remember to link.
- The JS receives the **assembly name** from C# (`typeof(RayforgeBridge).Assembly.GetName().Name`) instead
  of hardcoding it, and resolves both `Blazor.runtime` and `getDotnetRuntime(0)`.
- The canvas ID is generated per component; JS never assumes `"canvas"`.
- Package dependencies: `Raylib-cs [8.1.0, )`, `Microsoft.AspNetCore.Components.Web 10.0.x`.
- `buildTransitive` targets ship in the package; they must resolve paths relative to the package root, not
  the repo. Include them explicitly:

  ```xml
  <None Include="buildTransitive\**" Pack="true" PackagePath="buildTransitive\" />
  ```

  The wasm host file is `Rayforge.Web.targets`, not `.props`: the Razor SDK generates
  `buildTransitive/Rayforge.Web.props` for static web assets, and NuGet imports both conventions.

- Include `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md` and `PackageTags`
  (`raylib`, `blazor`, `wasm`, `game`).

Pack layout:

```
Rayforge.Web.0.1.0-preview.1.nupkg
├── lib/net10.0/Rayforge.Web.dll
├── staticwebassets/...                 # raylib.js, scoped CSS
├── buildTransitive/Rayforge.Web.targets
├── buildTransitive/Rayforge.Web.props   # generated by Razor: static web assets
├── README.md
├── LICENSE
└── THIRD-PARTY-NOTICES.md
```

### 6.4 Local development and publish

| Task | Command |
|---|---|
| Prereqs | `dotnet workload install wasm-tools` (installed on this machine during M0) |
| Run web | `dotnet run --project games/HelloWorld/HelloWorld.Web` |
| Run desktop | `dotnet run --project games/HelloWorld/HelloWorld.Desktop` |
| Publish | `dotnet publish games/HelloWorld/HelloWorld.Web -c Release -o artifacts/hello-world` |
| Serve published | any static server that sends `application/wasm` (see the note below) |
| Test | `dotnet test` |
| Pack + local feed | `./scripts/pack.sh` (writes `artifacts/packages` and `nuget-local/`) |
| Template smoke test | `./tests/template-smoke.sh` (packs, generates, builds both hosts, publishes) |

- `dotnet run` uses the Blazor DevServer and works with the asset copy target from M0. Debug is the fast inner
  loop; there is no need to publish for day-to-day work.
- **Release publish needs `EmccLinkOptimizationFlag=-O0`** until the `wasm-opt`/Binaryen 117 problem is solved
  (spike 0001, 4.3). The framework props set it, so hosts do not have to.
- **Hot reload is disabled for the canvas host.** `dotnet watch` can restart managed code while the native
  raylib window is still initialized, which cannot work. Rebuild + refresh the page. The base should detect
  a second `InitWindow` and show a clear "reload the page" message instead of crashing.
- Published output is `artifacts/hello-world/wwwroot/`. This is what gets deployed.
- `<base href="./" />` in `index.html` makes sub-path hosting work. No router means no `404.html` fallback is
  strictly needed, but CI copies `index.html` to `404.html` anyway for future routes.
- Add `.nojekyll` for GitHub Pages.
- Reference numbers for the Hello-World-sized app (spike 0002): **6.04 MB raw / 1.85 MB brotli, 45 files,
  ~1.1 s to first frame** on localhost without AOT. Use these as the baseline when checking regressions.

### 6.5 CI and deployment

`ci.yml` (on push/PR):

1. `actions/checkout`
2. `actions/setup-dotnet` with `10.0.x`
3. `dotnet workload install wasm-tools`
4. `dotnet restore`
5. `dotnet build -c Release --no-restore`
6. `dotnet test -c Release --no-build`
7. `dotnet publish games/HelloWorld/HelloWorld.Web -c Release -o artifacts/hello-world`
8. Assert key files exist (`_framework/dotnet.wasm`, `_content/Rayforge.Web/rayforge.js`, the bundled
   assets).
9. Upload `artifacts/hello-world/wwwroot` as an artifact.

`deploy-hello-world.yml` (main/tags): deploy the artifact to GitHub Pages. Add `.nojekyll`, copy
`index.html` to `404.html`, optionally a `CNAME`.

Caching: `~/.nuget/packages` and the workload install are the main costs. Cache NuGet; do not over-engineer
workload caching until build times are known.

### 6.6 Version matrix

| Component | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.4xx | `global.json`, `rollForward: latestFeature`; 10.0.402 verified |
| Blazor WebAssembly | 10.0.x | standalone, no server project |
| Raylib-cs | 8.1.0 | bindings for raylib 6.0; native archive ships in the package |
| raylib (native) | 6.0, Emscripten/GLFW + desktop RIDs | `runtimes/<rid>/native/` in the Raylib-cs package; nothing vendored |
| wasm-tools | 10.0.13 | `dotnet workload install wasm-tools` |
| Emscripten | 3.1.56 (from wasm-tools) | do not mix with a system EMSDK on `PATH` |
| Binaryen | 117 (from wasm-tools) | rejects `--enable-bulk-memory-opt`; see 4.3 in spike 0001 |
| xUnit | current | unit + architecture tests |
| Playwright | current | browser smoke test (M5) |


---

## 7. Hello World

Full spec and acceptance criteria: [HELLO-WORLD.md](HELLO-WORLD.md). Summary:

- **Goal:** prove the whole stack and the portability contract with the smallest interesting game.
- **Visual:** dark background, rotating 3D cube, "Hello, Rayforge!" text, a small logo texture, mouse
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
6. **Shaders:** add a GLSL ES **100** variant for the current `browser-wasm` archive (WebGL 1), keep
   desktop on 330, select at runtime via `Context.IsBrowser`. Use DevServer-safe extensions (`.txt`).
7. **Test desktop first** (fast, debuggable), then the web host.
8. **Profile** startup, memory and bundle size; choose Bundled vs Preload.
9. **Add the start overlay** for audio; verify pointer lock if the game needs it.
10. **Run** the compatibility checklist and the resize/mouse/DPR matrix.

Worked example for M3 (done): **DoomFire-Raylib** -> [`games/DoomFire/`](../games/DoomFire). Small,
shader-based, no server, no editor. Friction and verdict: [spike 0003](spikes/0003-doomfire-port.md).

Wolfrender is the large case (M6), not the first port. Known blockers:

- Raylib-cs 8.x / raylib 6.0 API changes (the base is already on 8.1.0; Wolfrender is not).
- 5 MB of assets and a hardcoded preload list.
- Custom pointer lock, frame pacing, debug console and `Game.Engine.*`/`Game.Core.*` namespaces.
- Desktop ImGui editor and `rlImGui` (desktop only; should not be referenced by the web host).
- Highscore server (out of scope for a static client).
- Custom shaders and per-face textures (GLSL ES variants).

---

## 9. Roadmap

The authoritative tracker, with per-milestone checklists, is [MILESTONES.md](MILESTONES.md). Summary:

| Milestone | Deliverable | Exit criteria | Status |
|---|---|---|---|
| **M0 - Spike** | Throwaway Blazor app proving the decided stack. | Renders + audio + assets + letterboxed mouse mapping; DevServer and static publish work; findings written. | **Done 2026-10-21** |
| **M0b - Upgrade probe** | M0 probe on Raylib-cs 8.1.0, package-provided native archive; upgrade workflow doc. | No undefined symbols; same behaviour; payload measured; `Raylib.props` and the vendored archive deleted. | **Done 2026-10-21** |
| **M1 - Skeleton** | Solution, 3 framework projects, `IGame`/`GameContext`, host boots to a Razor loading screen, CI build. | `dotnet build` green; dependency rules documented; no game logic. | **Done 2026-10-21** |
| **M2 - Hello World** | Base canvas component, JS bridge, desktop runner, Hello World game + hosts, assets, audio, resize, error/loading overlays. | All Hello World acceptance criteria pass; same game on desktop and web. | **Done 2026-10-21** |
| **M3 - Real port** | DoomFire port; porting guide validated; shader/MIME friction recorded. | DoomFire runs on web and desktop; friction list written. | **Done 2026-10-21** |
| **M4 - Template & packages** | `dotnet new rayforge-game`, packable `Rayforge.Core/Web/Desktop`, local feed, docs. | A new game can be created and run from the template without touching the base source. | **Done 2026-10-22** |
| **M5 - CI & deploy** | GitHub Actions publish, Pages deploy, Playwright smoke test, size/time report, AOT measurement. | Green CI on a clean runner; deployed demo URL; smoke test catches a blank canvas. | Planned |
| **M6 - Wolfrender port** (separate track) | Port Wolfrender onto Rayforge. | Playable in the browser; no Wolfrender-specific code in the base. | Planned |

M0 closed the decision gate; M0b settled the raylib baseline (8.1.0 / raylib 6.0) and absorbed the former M7.
M1-M4 are closed; M5 (CI and deployment) is the next action.

---

## 10. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Release publish breaks on `wasm-opt --enable-bulk-memory-opt` (Binaryen 117) | **Certain today** | Medium | `EmccLinkOptimizationFlag=-O0` in the framework props; solve properly in M5 (workload update / SDK band / patched rsp). |
| Asset build plumbing is fragile (`Link` -> 0-byte DevServer response) | High | Medium | Copy target before `ResolveStaticWebAssetsInputs` (spike 0001, 4.2); gitignore the generated folder; CI asserts a published asset is non-empty. |
| A second native archive sneaks in (vendored `.a` or duplicate `NativeFileReference`) | Low | High (link failure or silent ABI trap) | Never commit a native binary; CI greps `emcc-link.rsp` for exactly one `raylib.a`; undefined symbols fail the build (spike 0002, 4.1/4.2). |
| raylib 6.0 API changes make porting existing games expensive | High for Wolfrender | Medium | The base is already on 8.1.0; adapt Wolfrender in M6 and record the diff; DoomFire in M3 is the small rehearsal. |
| `wasm-tools` / SDK / Emscripten mismatch | Medium | Medium | Pin SDK in `global.json`; CI installs the workload fresh; record the versions in `MILESTONES.md`. |
| AOT build time/size is unacceptable | Medium | Medium | Interpreter by default; AOT measured in M5 before any policy change. |
| Asset memory blow-up (MEMFS + decoded textures/audio) | Medium | Medium | Bundled only for small games; Preload groups for large ones; memory growth is on by default but the initial 32 MB heap still bounds startup; measure at M3 (DoomFire) and M6 (5 MB of assets). |
| Blazor render and game loop compete for the main thread | Low-Medium | Medium | No per-frame Blazor; overlays only; measure frame time with the diagnostics overlay. |
| Pointer lock/audio/fullscreen gestures behave differently across browsers | Medium | Medium | Start overlay; test Chrome/Firefox/Safari; degrade gracefully; document unsupported features. |
| HiDPI/mouse mapping bugs | Low now | Medium | Fit mode + `SetMouseScale` verified at 3 CSS scales and DPR 2; unit-test layout math; keep the manual matrix. |
| Hot reload / remount double-inits raylib | High (with `dotnet watch`) | Low | Disable hot reload for the canvas; guard against a second init; show "reload" message. |
| GitHub Pages sub-path breaks absolute URLs | Low | Medium | `<base href="./">`; build JS module + asset URLs from `NavigationManager.BaseUri`; test under `/repo/`. |
| Trimming/AOT breaks reflection-based game code | Medium | Medium | Prefer source-generated JSON; document trim warnings; `TrimmerRootAssembly` escape hatch per game. |
| Base creeps into an engine | High over time | High | Explicit non-goals; keep `Rayforge.Core` tiny; reject PRs that add scene/physics/editor features. |
| License/attribution mistake | Low | Medium | MIT for our code; keep raylib/raylib-cs notices; no Wolfrender code copied. |

---

## 11. Decisions (recorded)

These were decided in review on 2026-10-21, before M0:

1. **Name:** Rayforge.
2. **Structure:** Alternative A (layered monorepo), evolving into C (packages) at M4.
3. **Raylib baseline:** Raylib-cs 8.1.0 / raylib 6.0 from the package; no vendored native, no custom props
   (M0b, spike 0002).
4. **Desktop parity in scope from day one:** yes.
5. **Canvas mode:** both Fit and Fill, Fit by default.
6. **Asset mode:** Bundled (semantics corrected by M0, see 5.7).
7. **AOT:** not in v1; revisit in M5.
8. **License:** MIT.
9. **First real port:** DoomFire-Raylib (M3).
10. **Remote:** public GitHub repository `kenwi/Rayforge`.
11. **Milestones:** tracked in [MILESTONES.md](MILESTONES.md).
12. **Wolfrender:** M6, separate track.
13. **Multi-game host:** no (one host per game; gallery later).

Still open (do not block M1):

| Question | Needed by |
|---|---|
| NuGet package IDs (`Rayforge.Core` etc.) - checked free on nuget.org 2026-10-22; not reserved | M4 |
| Default target browsers (Chrome + Firefox first, Safari best-effort; mobile out of scope?) | M2 |
| Whether the desktop RIDs other than `linux-x64` load their package natives (Windows, macOS x64/arm64) | M2/M5 |
| Whether `dotnet workload update` fixes the Release `wasm-opt` failure | M5 |

---

## 12. References

- Raylib-cs 8.1.0 (the baseline): <https://github.com/raylib-cs/raylib-cs/releases/tag/v8.1.0>
  - browser-wasm support, `runtimes/browser-wasm/native/raylib.a`, `buildTransitive/Raylib-cs.targets`
  - web example: `Examples/Web/{README.md,Host.cs,main.js,index.html,scaleUtils.js}`
- Raylib 6.0: <https://github.com/raysan5/raylib/releases/tag/6.0>
- Blazor WebAssembly native dependencies:
  <https://learn.microsoft.com/aspnet/core/blazor/webassembly-native-dependencies>
- Blazor.Raylib (MIT): <https://github.com/trucidare/Blazor.Raylib>
- DotnetRaylibWasm: <https://github.com/stanoddly/DotnetRaylibWasm>
- Reference implementation (behaviour only, no code copied): `~/Work/Wolfrender.Blazor.Raylib/OVERVIEW.md`,
  `Components/Raylib.razor.cs`, `wwwroot/js/raylib.js`, `Raylib.props`
- M0 evidence: [spikes/0001-raylib-cs-blazor-wasm.md](spikes/0001-raylib-cs-blazor-wasm.md),
  [`spike/m0-raylib-blazor/`](../spike/m0-raylib-blazor)
- M0b evidence: [spikes/0002-raylib-cs-8.1.0.md](spikes/0002-raylib-cs-8.1.0.md),
  [`spike/m0b-raylib-upgrade/`](../spike/m0b-raylib-upgrade), [UPGRADING-RAYLIB.md](UPGRADING-RAYLIB.md)

---

## 13. Review checklist

- [x] Name approved: Rayforge.
- [x] Structure chosen: A (layered monorepo).
- [x] Raylib baseline agreed: 8.1.0 / raylib 6.0 from the package; vendored `raylib.a` dropped (M0b).
- [x] Desktop host in scope.
- [x] Canvas mode (both, Fit default) and asset mode (Bundled) agreed.
- [x] AOT policy agreed (later, measured in M5).
- [x] License (MIT) and remote (`kenwi/Rayforge`, public) decided.
- [x] M0 spike completed and accepted: GO.
- [x] M0b upgrade probe completed and accepted: GO; M1 starts on Raylib-cs 8.1.0.
- [ ] M1 skeleton approved as the next action.


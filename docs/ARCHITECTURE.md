# Architecture

The rules that keep one game library runnable on the web and on the desktop. Scope: through M4 (template and
packages). The milestone checklists live in [MILESTONES.md](MILESTONES.md); the rationale is in
[PLAN.md](PLAN.md).

## Project graph

```text
                     games/<Game>/<Game>.Game        (net10.0, Raylib-cs + Rayforge.Core only)
                        ^                  ^
                        |                  |
     games/<Game>/<Game>.Web        games/<Game>/<Game>.Desktop
            |                              |
     src/Rayforge.Web               src/Rayforge.Desktop
            \                              /
             ---------> src/Rayforge.Core <---------
```

`Rayforge.Core` owns the contracts. `Rayforge.Web` is the Blazor WebAssembly side (canvas, JS bridge,
asset preload). `Rayforge.Desktop` is the native side (window, loop, cursor). A game library knows about
neither host.

## Dependency rules

1. **`Rayforge.Core` is platform-neutral.** No Blazor, no `Microsoft.JSInterop`, no
   `System.Runtime.InteropServices.JavaScript`, no reference to `Rayforge.Web` or `Rayforge.Desktop`.
   It does reference `Raylib-cs`, because the contract hands raylib types (`ConfigFlags`) to the host and
   games draw with raylib directly.
2. **A game library never references a host.** `<Game>.Game` may use `Rayforge.Core` and `Raylib-cs`; it may
   not use Blazor, JS interop, `Rayforge.Web` or `Rayforge.Desktop`. If a game needs a host capability, it
   goes through `GameContext` (`IRuntimeHost`, `IMouseCapture`, `IGameLog`, `GameDiagnostics`).
3. **Hosts do not reference each other.** Web and desktop share only `Rayforge.Core`.
4. **Hosts stay thin.** A host configures `GameOptions`, registers services and calls one entry point. Game
   rules, scene setup and asset names belong in the game library.

These are enforced by [`tests/Rayforge.Tests/DependencyRuleTests.cs`](../tests/Rayforge.Tests/DependencyRuleTests.cs),
which inspects `Assembly.GetReferencedAssemblies()` for each framework and game assembly. Adding a forbidden
reference fails `dotnet test` — no review required to catch it.

## One raylib context per page

raylib/emscripten binds a WebGL context to the canvas on `InitWindow`. Closing and re-initializing in the same
document is not supported, and a disposed canvas cannot be re-bound. Therefore:

- The web host has **no router** and is not a website: one full-screen surface per page.
- `RayforgeCanvas` has a static guard: a second live instance renders an error panel instead of a canvas.
- `DisposeAsync` releases the JS binding only; it never re-initializes raylib.
- Restarting a game means reloading the page. On desktop, `DesktopRunner` creates exactly one window and one
  game instance per process.

## Boot and frame loop

### Web

```text
wwwroot/index.html
  -> blazor.webassembly.js
  -> Program.cs        AddRayforge<HelloWorldGame>(...)
  -> App.razor         <ErrorBoundary><RayforgeCanvas /></ErrorBoundary>
  -> RayforgeCanvas.OnAfterRenderAsync
       1. JSHost.ImportAsync("rayforge", "<base>/_content/Rayforge.Web/rayforge.js")
          (absolute URL: a relative one resolves against _framework/)
       2. rayforge.js init(canvasId, mode, title) -> binds Blazor.runtime.Module.canvas,
          installs ResizeObserver/fullscreen/visibility/context-loss listeners, returns host metrics
       3. CanvasLayout.Fit|Fill(...) -> setCanvasCssSize(...)
       4. GET assets/rayforge.assets.json, then preload(paths) -> fetch + FS.writeFile into MEMFS
       5. state ReadyToStart: the "Click to play" button
       6. click -> InitWindow + InitAudioDevice + game.Initialize  (all inside the gesture)
       7. RayforgeInterop.Start(handle, "Rayforge.Web", targetFps): resolves the [JSExport]s, starts rAF
       8. state Running: the overlay is removed; only the game draws
       9. failure at any step -> state Failed with the exception and a Reload button
```

Per frame, JS clamps `dt` (0.1 s), skips while the tab is hidden and calls exactly one synchronous export:

```text
rAF(now) -> RayforgeBridge.UpdateFrame(handle, dt)
            -> RayforgeCanvas.Tick(dt)
               -> game.Update(dt); game.Draw();        (exceptions -> stop + overlay + teardown)
            -> false stops the loop
```

Resize and fullscreen never touch the backing store math in JS: the `ResizeObserver` calls
`RayforgeBridge.OnLayoutChanged(handle, hostW, hostH)`; C# recomputes `CanvasLayout`, calls back
`setCanvasCssSize` (Fit) or `setBackingSize` + `SetWindowSize` + `game.OnResize` (Fill), and applies
`SetMouseScale`. `InitWindow` assigns its own CSS size, so the layout is re-applied right after it and before
the first frame.

Teardown: `DisposeAsync` stops the loop, lets the game unload its own resources, closes the audio device and the
window, then releases the JS binding. Crashes go through the same `Shutdown` and end in the overlay, never in a
frozen canvas.

### Desktop

```text
Program.cs
  -> DesktopRunner.Run<HelloWorldGame>(options)
       SetCurrentDirectory(AppContext.BaseDirectory)   (relative "assets/..." paths)
       SetConfigFlags -> InitWindow -> SetExitKey(Null) -> optional InitAudioDevice
       -> DesktopContext -> game.Initialize
       -> loop: Update / Draw, resize polling -> OnResize, mouse delta accumulation
       -> game.Dispose -> CloseAudioDevice -> CloseWindow
```

`SetExitKey(Null)` is deliberate: Escape is the game's decision, not the host's. Running from the app directory
makes the assets copied next to the executable resolve regardless of where the IDE or shell started the process.

### Traps encoded in the host

- A Razor class library needs `_Imports.razor` with `@using Microsoft.AspNetCore.Components.Web`; without it
  `@onclick` compiles to literal markup and the button does nothing.
- Calling a `[JSImport]` before `JSHost.ImportAsync` succeeded **aborts the .NET runtime** (not a catchable
  exception). The host tracks `_moduleImported` and never calls into JS before it.
- `InitWindow` sets the canvas CSS size to the logical size, which destroys the letterbox at any other aspect
  ratio. Re-apply the layout immediately after `InitWindow`.
- `<base href="./" />` (not `/`) so the publish works under a sub-path. Everything else resolves against
  `NavigationManager.BaseUri`/`document.baseURI`.
- A keydown+keyup inside a single frame is invisible to `IsKeyPressed` (input is sampled once per frame). This
  matters for automated tests; a human tap is several frames long.

## Asset pipeline

Assets live once, in `games/<Game>/Assets/`, and are **never loaded in place**. Both hosts import
`games/<Game>/Assets.props`, which branches on `RayforgeHost`:

| Host | What happens |
|---|---|
| `Web` | Copy to `<Host>/wwwroot/assets/` **before** `ResolveStaticWebAssetsInputs`, then register the copies as `Content`. The same target writes `rayforge.assets.json`, the build-generated list. |
| `Desktop` | `Content` + `Link="assets/..."`, copied to output and publish. raylib opens the relative paths normally. |

Why not `Content` + `Link` on the web? Spike 0001: the DevServer answers 200 with 0 bytes. The copy target is
the workaround, and it is why `wwwroot/assets/` is generated output and gitignored.

Why a generated list? So nobody maintains an array in C# or JS. `RayforgeCanvas` fetches it and `rayforge.js`
writes exactly those files into MEMFS before the game starts. `WasmFilesToIncludeInFileSystem` is not used -
Blazor ignores it for this purpose.

The web target wipes `wwwroot/assets/` before copying: it is generated, gitignored output, and stale files
(a replaced `blip.wav`, old `.br` siblings) must not leak into a publish.

## wasm build flags

`src/Rayforge.Web/buildTransitive/Rayforge.Web.targets` is the single source of truth:

- `WasmBuildNative`, `WasmEnableExceptionHandling`, `InvariantGlobalization`;
- `EmccLinkOptimizationFlag=-O0` when unset — **temporary**, see spike 0001 4.3 / spike 0002 4.4 (Binaryen 117
  rejects a flag used by `-O2`); owned by M5;
- nothing raylib-specific: the Raylib-cs package supplies `raylib.a`, `-sUSE_GLFW=3` and the RID natives.

In this repo `Directory.Build.targets` imports that file when `RuntimeIdentifier == browser-wasm`
(`Directory.Build.props` runs before the csproj sets the RID). Packaged consumers get the same file through
`buildTransitive`. The file is a `.targets`, not a `.props`: the Razor SDK already generates
`buildTransitive/Rayforge.Web.props` for static web assets, and NuGet imports both conventions. Never
duplicate these flags per host.

`WasmAllowUndefinedSymbols` is deliberately **not** set: an unresolved native symbol must fail the build. That
is the ABI gate.

## Packages and the template (M4)

The framework projects are packable; game libraries and hosts are not. `dotnet pack` produces one nupkg per
framework project at version `$(RayforgeVersion)` (`0.x-preview.N`):

| Package | Contents |
|---|---|
| `Rayforge.Core` | Contracts assembly; depends on `Raylib-cs` |
| `Rayforge.Web` | RCL assembly, `rayforge.js`, scoped CSS, static web assets, `buildTransitive/Rayforge.Web.targets` |
| `Rayforge.Desktop` | Desktop runner assembly; depends on `Rayforge.Core` |

Every package carries `README.md`, `LICENSE` and `THIRD-PARTY-NOTICES.md`. None carries a native binary; the
`Raylib-cs` dependency brings `raylib.a` and the desktop RIDs. `Rayforge.Core` and `Rayforge.Desktop` are
normal library dependencies of the hosts; `Rayforge.Web` also contributes its static web assets and the wasm
host flags.

The in-repo games keep `ProjectReference`s for a fast inner loop. The consumer path is deliberately different:
[`templates/rayforge-game`](../templates/rayforge-game) has no reference to framework source and restores the
packages from a local feed.

```bash
./scripts/pack.sh                    # artifacts/packages -> nuget-local/
dotnet new install ./templates/rayforge-game
dotnet new rayforge-game -n MyGame --RayforgeFeed "$PWD/nuget-local"
dotnet build MyGame/MyGame.slnx -c Release
```

`NuGet.config` at the repository root and the generated project's `NuGet.config` both declare a
`rayforge-local` folder source. `tests/template-smoke.sh` repeats the consumer flow from a clean temporary
directory, builds both hosts, checks the nupkg contents, and publishes the web host.

## UI

Player-facing UI is game-owned and raylib-drawn (or a small managed immediate-mode layer); Blazor owns only
loading, start, errors and low-frequency shell UI. Dear ImGui is not a framework dependency. The investigation
and the fallback plan are in [UI-STRATEGY.md](UI-STRATEGY.md).

## What M2 deliberately does not do

- No pointer-lock demo: `BrowserMouseCapture` is wired and correct, but the Hello World demo has no
  first-person camera (stretch goal).
- No streaming/progress asset mode: everything is preloaded before the first frame (Bundled only).
- No AOT (M5); the Release publish is trimmed, and the `[JSExport]` surface survives it.
- No router and no multiple canvases: one raylib context per page, reload to restart.
- Firefox/Safari unverified (only Chromium is installed on the development machine).

## Verifying locally

```bash
dotnet workload install wasm-tools            # once
dotnet build Rayforge.slnx
dotnet test Rayforge.slnx
dotnet run --project games/HelloWorld/HelloWorld.Web        # http://localhost:5xxx
dotnet run --project games/HelloWorld/HelloWorld.Desktop    # needs a display
dotnet publish games/HelloWorld/HelloWorld.Web -c Release -o artifacts/hello-world
./tests/template-smoke.sh                                   # packed packages + generated game
```

The publish log must say `Linking with emcc with -O0`, and `artifacts/hello-world/wwwroot/assets/` must contain
the real asset bytes plus `rayforge.assets.json`. Serve with any server that sends `application/wasm`.

Firefox/Safari are **not** verified yet: only Chromium is installed on the development machine.

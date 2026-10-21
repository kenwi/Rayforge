# Architecture

The rules that keep one game library runnable on the web and on the desktop. Scope: M1 (skeleton). The
milestone checklists live in [MILESTONES.md](MILESTONES.md); the rationale is in [PLAN.md](PLAN.md).

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

## Boot sequences (as of M1)

### Web

```text
wwwroot/index.html
  -> blazor.webassembly.js
  -> Program.cs        AddRayforge<HelloWorldGame>(...)
  -> App.razor         <RayforgeCanvas />
  -> RayforgeCanvas.OnAfterRenderAsync
       1. JSHost.ImportAsync("rayforge", "<base>/_content/Rayforge.Web/rayforge.js")
          (absolute URL: a relative one resolves against _framework/)
       2. rayforge.js init(canvasId)  -> binds Blazor.runtime.Module.canvas, measures the host
       3. CanvasLayout.Fit(logical, viewport) -> setCanvasCssSize(...)
       4. GET assets/rayforge.assets.json        (build-generated; see below)
       5. ready badge, or the error overlay with the exception
```

M1 stops after step 5. M2 adds MEMFS asset preload, the `requestAnimationFrame` loop and the synchronous frame
bridge. The canvas is intentionally black in M1: the badge reports the mode, the asset count and the CSS size.

### Desktop

```text
Program.cs
  -> DesktopRunner.Run<HelloWorldGame>(options)
       SetConfigFlags -> InitWindow -> SetExitKey(Null) -> optional InitAudioDevice
       -> DesktopContext -> game.Initialize
       -> loop: Update / Draw, resize polling -> OnResize, mouse delta accumulation
       -> game.Dispose -> CloseAudioDevice -> CloseWindow
```

`SetExitKey(Null)` is deliberate: Escape is the game's decision, not the host's.

## Asset pipeline

Assets live once, in `games/<Game>/Assets/`, and are **never loaded in place**. Both hosts import
`games/<Game>/Assets.props`, which branches on `RayforgeHost`:

| Host | What happens |
|---|---|
| `Web` | Copy to `<Host>/wwwroot/assets/` **before** `ResolveStaticWebAssetsInputs`, then register the copies as `Content`. The same target writes `rayforge.assets.json`, the build-generated list. |
| `Desktop` | `Content` + `Link="assets/..."`, copied to output and publish. raylib opens the relative paths normally. |

Why not `Content` + `Link` on the web? Spike 0001: the DevServer answers 200 with 0 bytes. The copy target is
the workaround, and it is why `wwwroot/assets/` is generated output and gitignored.

Why a generated list? So nobody maintains an array in C# or JS. M1 fetches it and reports the count; M2 will
write exactly those files into MEMFS. `WasmFilesToIncludeInFileSystem` is not used - Blazor ignores it for
this purpose.

## wasm build flags

`src/Rayforge.Web/buildTransitive/Rayforge.Web.props` is the single source of truth:

- `WasmBuildNative`, `WasmEnableExceptionHandling`, `InvariantGlobalization`;
- `EmccLinkOptimizationFlag=-O0` when unset — **temporary**, see spike 0001 4.3 / spike 0002 4.4 (Binaryen 117
  rejects a flag used by `-O2`); owned by M5;
- nothing raylib-specific: the Raylib-cs package supplies `raylib.a`, `-sUSE_GLFW=3` and the RID natives.

In this repo `Directory.Build.targets` imports that file when `RuntimeIdentifier == browser-wasm`
(`Directory.Build.props` runs before the csproj sets the RID). Packaged consumers get the same file through
`buildTransitive` at M4. Never duplicate these flags per host.

`WasmAllowUndefinedSymbols` is deliberately **not** set: an unresolved native symbol must fail the build. That
is the ABI gate.

## What M1 deliberately does not do

- No game loop on the web, no `InitWindow` yet; the desktop loop exists and runs the placeholder game.
- No asset preload into MEMFS (the manifest is fetched and counted only).
- No resize observer after boot; the layout is computed once at boot. M2 adds it.
- No pointer lock, fullscreen or audio on the web.
- No AOT, no trimming audit beyond the default Release publish.
- No router, no multiple canvases.

## Verifying locally

```bash
dotnet workload install wasm-tools            # once
dotnet build Rayforge.slnx
dotnet test Rayforge.slnx
dotnet run --project games/HelloWorld/HelloWorld.Web        # http://localhost:5xxx
dotnet run --project games/HelloWorld/HelloWorld.Desktop    # needs a display
dotnet publish games/HelloWorld/HelloWorld.Web -c Release -o artifacts/hello-world
```

The publish log must say `Linking with emcc with -O0`, and `artifacts/hello-world/wwwroot/assets/` must contain
the real asset bytes plus `rayforge.assets.json`. Serve with any server that sends `application/wasm`.

Firefox/Safari are **not** verified yet: only Chromium is installed on the development machine.

# Milestones

Living checklist. Tick items as they are completed; add the date and the evidence link when a milestone closes.
The roadmap rationale lives in [PLAN.md](PLAN.md) section 9; this file is the tracker.

Legend: `[x]` done, `[ ]` planned, **(blocked)** needs something external.

## Status overview

| # | Milestone | Scope | Status |
|---|---|---|---|
| M0 | Spike | Prove Raylib-cs 7.0.2 + `raylib.a` in a fresh Blazor app | **Done** - 2026-10-21 |
| M0b | Upgrade probe | Raylib-cs 8.1.0 / raylib 6.0 from the package; drop the vendored `raylib.a` | **Done** - 2026-10-21 |
| M1 | Skeleton | Solution, framework projects, contracts, asset plumbing, CI build | **Done** - 2026-10-21 |
| M2 | Hello World | Base canvas + JS bridge + desktop runner + demo game on both hosts | **Done** - 2026-10-21 |
| M3 | First real port | DoomFire-Raylib on web and desktop; porting guide validated | **Done** - 2026-10-21 |
| M4 | Template & packages | `dotnet new` template, packable framework, local feed | **Done** - 2026-10-22 |
| M5 | CI & deploy | GitHub Actions, Pages deploy, Playwright smoke test, size report | **Done** - 2026-10-23 |
| M6 | Wolfrender port | Separate track: port Wolfrender onto the base | Not started |

M0 was the decision gate and is closed; M0b settled the raylib baseline and removed the former M7. M1 built the
skeleton; M2 delivered Hello World; M3 validated the porting guide on DoomFire; M4 made the framework
packable and added the game template; M5 made the pipeline deploy the tested demo. M6 is the next track.

---

## M0 - Spike

**Goal:** answer "does the decided stack work at all, in a clean-room project?"
**Deliverable:** a throwaway Blazor app plus written findings.
**Result:** [spike 0001](spikes/0001-raylib-cs-blazor-wasm.md), code in [`spike/m0-raylib-blazor/`](../spike/m0-raylib-blazor).

- [x] Install `wasm-tools` and confirm a Blazor WASM build works on this machine
- [x] Link the vendored `raylib.a` (raylib 5.6-dev) into a fresh Blazor project via `NativeFileReference`
- [x] Bind a canvas to `Blazor.runtime.Module['canvas']` without hardcoded ids
- [x] Drive the game from `requestAnimationFrame` with one synchronous `[JSExport]` per frame
- [x] Render 3D (camera, depth) and 2D (text, lines, circles)
- [x] Load a PNG texture and a WAV sound from the virtual filesystem
- [x] `InitAudioDevice` + `LoadSound` + `PlaySound`
- [x] Fit-mode letterboxing with `SetMouseScale`; marker aligned at CSS scale 1.0 / 1.42 / 1.83 and DPR 2
- [x] `dotnet run` (DevServer) serves a working app
- [x] `dotnet publish` + plain static server serves a working app
- [x] Error overlay instead of a black canvas when boot fails
- [x] Record failures, workarounds, measurements and plan corrections
- [ ] Firefox and Safari verification **(blocked: only Chromium is installed here)**

**Closed:** 2026-10-21. **Verdict:** GO. Four plan corrections were folded into `PLAN.md` (asset mode semantics,
asset copy target, Release `-O0` link workaround, absolute JS module URLs).

The 7.0.2 + vendored `raylib.a` baseline recorded here was superseded by M0b the same day. The spike stays as
the record of the technique; the project runs on Raylib-cs 8.1.0.

---

## M0b - Raylib 8.1.0 upgrade probe

**Goal:** decide whether the official `browser-wasm` package replaces the vendored `raylib.a` before M1 bakes
the old baseline into the skeleton.
**Deliverable:** the M0 probe re-run on Raylib-cs 8.1.0, plus the repeatable upgrade procedure.
**Result:** [spike 0002](spikes/0002-raylib-cs-8.1.0.md), code in
[`spike/m0b-raylib-upgrade/`](../spike/m0b-raylib-upgrade).

- [x] Link the package's `runtimes/browser-wasm/native/raylib.a`; no `Raylib.props`, no vendored archive
- [x] Verify the response file contains exactly one `raylib.a` and one `-sUSE_GLFW=3`
- [x] Build with **zero** undefined-symbol warnings (M0 had three) and without `WasmAllowUndefinedSymbols`
- [x] Run 3D + 2D + texture + audio + Fit letterboxing + DPR 2 on 8.1.0
- [x] Assert `IsTextureValid` / `IsSoundValid` in the probe (the helpers exist in both versions)
- [x] Confirm the Release `wasm-opt` failure is toolchain-only and unaffected by the upgrade
- [x] Prove the custom `ALLOW_MEMORY_GROWTH` / `FORCE_FILESYSTEM` / `TOTAL_MEMORY` flags are unnecessary
- [x] Measure payload against the 7.0.2 baseline (6.04 MB vs 6.21 MB raw; 1.85 MB vs 1.88 MB brotli)
- [x] Write [UPGRADING-RAYLIB.md](UPGRADING-RAYLIB.md) for the next upgrade
- [ ] Desktop host on 8.1.0 (the package ships the natives; the host itself is M2)
- [ ] Firefox and Safari **(blocked: only Chromium is installed here)**

**Closed:** 2026-10-21. **Verdict:** GO. Rayforge pins Raylib-cs 8.1.0 / raylib 6.0 from M1 on; M7 is removed.

---

## M1 - Skeleton

**Goal:** a buildable, empty framework with hard boundaries and no game logic.
**Exit criteria:** `dotnet build` is green, dependency rules are documented and enforced by a test, the web host
boots to a Razor loading screen and fails gracefully.

- [x] Create `Rayforge.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`
- [x] `src/Rayforge.Core`: `IGame`, `GameBase`, `GameContext`, `GameOptions`, `CanvasScaleMode`, `AssetMode`,
      `IMouseCapture`, `IRuntimeHost`, `IGameLog`, `GameDiagnostics` - no Blazor, no JavaScript interop
- [x] `src/Rayforge.Web`: Razor Class Library skeleton, `wwwroot/rayforge.js`, scoped CSS, DI entry point
- [x] `src/Rayforge.Desktop`: `DesktopRunner` + desktop services
- [x] `games/HelloWorld/HelloWorld.Game`: empty `IGame` implementation that compiles against `Rayforge.Core`
- [x] `games/HelloWorld/HelloWorld.Web` + `HelloWorld.Desktop`: hosts that boot and show "loading"/"no game" states
- [x] Asset plumbing: `Assets.props` + the `wwwroot/assets` sync target from spike 0001, plus a build-generated
      asset list for the web host (no hand-maintained array)
- [x] Framework `buildTransitive/Rayforge.Web.targets` with `WasmBuildNative`, `EmccLinkOptimizationFlag=-O0`
      (documented as temporary) and `InvariantGlobalization` - **no** raylib link flags, no
      `NativeFileReference`, no `WasmAllowUndefinedSymbols` (the package supplies the native archive; see
      spike 0002)
- [x] Architecture test: `.Game` references no Blazor/JS assembly; `Core` references no web assembly
- [x] `tests/Rayforge.Tests` project with the first unit tests for `CanvasLayout`
- [x] `.github/workflows/ci.yml`: restore, build, test (no deploy yet)
- [x] Document the dependency rules and the "one raylib context per page" rule

**Notes:** `wwwroot/assets/` is generated output and stays gitignored. There is no `raylib.a` in the repo:
`Raylib-cs 8.1.0` brings the native archive for every RID. Do not set `RuntimeIdentifier=browser-wasm` on
`Rayforge.Core` or the game library, or the package's native reference may be requested outside the web host.

**Result:** solution builds with 0 warnings; 25 unit tests pass (CanvasLayout, dependency rules, GameBase
lifecycle, option defaults). The web host boots in a headless Chromium check against both the DevServer and a
Release static publish: 1280x720 viewport -> 1280x720 CSS, 900x900 -> 900x506.25 at y=196.875, 2 assets listed,
no console errors; blocking `rayforge.js` produces the error overlay instead of a black page. The desktop host
opened a 1280x720 window, initialized the placeholder game with `browser=False`, and ran the loop without errors.
Release publish logs `Linking with emcc with -O0` and ships real asset bytes plus `rayforge.assets.json`. The
dependency rules and the one-context rule are written down in [ARCHITECTURE.md](ARCHITECTURE.md).

**Open:** the CI workflow has not executed yet (no push since it was added). Firefox/Safari remain unverified
(only Chromium installed). M1 deliberately has no web frame loop, no MEMFS preload and no post-boot resize.

**Closed:** 2026-10-21.

---

## M2 - Hello World

**Goal:** the full pipeline with a real game on both hosts.
**Exit criteria:** every acceptance criterion in [HELLO-WORLD.md](HELLO-WORLD.md) passes.

- [x] `RayforgeCanvas` component: `OnAfterRenderAsync` boot, loading/error overlays, error boundary, teardown
- [x] `rayforge.js`: canvas binding, rAF loop, resize, fullscreen, asset preload into MEMFS, visibility pause
- [x] `RayforgeBridge`: synchronous `[JSExport]` frame entry, crash capture, stop-on-error
- [x] `CanvasLayout`: pure Fit/Fill math, unit-tested (CSS size, mouse scale, letterbox offsets) *(math + tests landed in M1)*
- [x] `BrowserMouseCapture`: pointer lock, `movementX/Y` accumulation, scale applied *(implemented; demo is a stretch goal)*
- [x] `BrowserRuntimeHost`: fullscreen, quit, `document.title`
- [x] `Rayforge.Desktop`: runner, cursor capture, resize polling, teardown *(audio enabled in M2)*
- [x] Hello World game: cube, text, logo texture, font, beep, rings, diagnostics, resize, fullscreen
- [x] Generated assets: `logo.png`, `blip.ogg`, `ui.ttf` with license notes in `Assets/README.md`
- [x] Diagnostics overlay (F3/D) fed by host counters, never per-frame Blazor renders
- [ ] Pointer-lock, fullscreen and audio verified in Chrome and Firefox **(blocked: only Chromium installed; fullscreen and audio verified there)**
- [x] Record publish size, load time and memory in `HELLO-WORLD.md`

**Notes:** the browser runtime is `src/Rayforge.Web/wwwroot/rayforge.js`; C# owns layout, JS owns the loop and
the DOM. `Assets.props` now wipes `wwwroot/assets` on every build so removed assets cannot linger in a publish.
`<base href="./" />` is required for sub-path hosting.

**Result:** Hello World runs identically on both hosts. Headless Chromium (DevServer and a Release static
publish, at `/` and under `/hello-world/`) checked: three assets in MEMFS, click-to-play, 60 fps loop, Space
pause/resume, D diagnostics, F fullscreen, letterboxing at 900x900 (900x506.25 at y=196.875), mouse marker
within ~1 px at DPR 1 and 2, blocked-module error, deliberate crash (X) with teardown + Reload, audio unlocked
by the click. Desktop loaded all assets, `Rayforge Sans` and audio, and followed compositor resizes
(957x1162 and 1920x1200).

Release publish: 6.63 MB raw / 2.05 MB brotli, ~1.0 s to the start button, 9.2 MB JS heap + 38.4 MiB wasm heap.
Details in [HELLO-WORLD.md](HELLO-WORLD.md) section 7.

**Traps found (now handled):** an RCL needs `_Imports.razor` with `@using Microsoft.AspNetCore.Components.Web`
or `@onclick` compiles to literal markup; `InitWindow` overwrites the canvas CSS size, so the letterbox must be
re-applied before the first frame; calling a `[JSImport]` before its module is imported aborts the .NET runtime;
a keydown+keyup inside one frame is invisible to `IsKeyPressed`; the desktop host must run from its app directory
for relative assets to resolve.

**Open:** Firefox/Safari (not installed); pointer lock is implemented but not shown in the demo; AOT off (M5).

**Closed:** 2026-10-21.

---

## M3 - First real port: DoomFire-Raylib

**Goal:** prove the porting guide on a game the framework did not design for.
**Exit criteria:** DoomFire runs on web and desktop from one `net10.0` game library; friction list written down.
**Result:** [`games/DoomFire/`](../games/DoomFire), [spike 0003](spikes/0003-doomfire-port.md),
[PORTING-GUIDE.md](PORTING-GUIDE.md) validated.

- [x] Extract DoomFire into `games/DoomFire/DoomFire.Game` (no Blazor, no window/loop)
- [x] Shaders: GLSL ES 100 / GLSL 330 selected at runtime via `Context.IsBrowser`, not `#if BROWSER`
- [x] Web + desktop hosts, assets under `Assets/`, `Assets.props`
- [x] Preload/manifest mode (groups + progress) if asset volume justifies it *(skipped: 8 tiny shader
      files; Bundled is enough)*
- [x] Update `PORTING-GUIDE.md` with every real friction point
- [x] Confirm the "under ~50 lines of host-specific changes" expectation (or explain why not)
- [x] Pointer-lock spike (0004) if any input needs it; otherwise defer to M6 *(deferred: keyboard-only)*

**Notes:** source was `~/Work/DoomFire-Raylib` (embedded GLSL 330, blocking loop, auto-fullscreen). The
port keeps `Fire/` as a plain library; hosts only set `GameOptions` (Fill, 960x540, no audio). Shaders live
under `Assets/shaders/{gl330,es100}/*.txt` and are chosen at runtime.

**Host-specific game changes:** well under 50 lines - split `Main` into `IGame`, `Escape` ->
`RequestQuit`, `F` -> `ToggleFullscreen`, host `dt` instead of `GetFrameTime`, shader folder from
`IsBrowser`. The ES 100 shader rewrite is asset work, not host code. Each host `Program.cs` is ~20 lines.

**Friction found (now in the porting guide):** DevServer 404 for unknown MIME (`.frag`/`.vert`);
Raylib-cs 8.1.0 `browser-wasm` is WebGL 1 / GLSL ES 100 (not 300); no `textureSize()` on ES 100
(`heatSize` uniform); no dynamic uniform-array indexing in WebGL 1 fragment shaders (loop-index pattern).

**Result:** desktop smoke (shaders compile, fire + HUD, compositor resize). Web DevServer + headless
Chromium: 8 assets / ~6 KB into MEMFS, `es100` shaders compile, Fill canvas 1280x720, loop runs without a
start gesture. 27 unit tests pass (dependency rules cover DoomFire.Game).

**Open:** Firefox/Safari unverified; optional DevServer MIME map for `.frag`/`.vert`; WebGL 2 / ES 300 if a
future Raylib-cs package switches.

**Closed:** 2026-10-21.

---

## M4 - Template and packages

**Goal:** a new game can be created without touching the framework source.
**Exit criteria:** `dotnet new rayforge-game` produces a project that builds and runs against packed packages.

- [x] `IsPackable=true` for `Rayforge.Core`, `Rayforge.Web`, `Rayforge.Desktop`; pack `buildTransitive`
      targets, README, LICENSE and `THIRD-PARTY-NOTICES` (raylib / Raylib-cs are Zlib) - no native binary, the
      Raylib-cs dependency brings it
- [x] Versioning scheme (`0.x-preview.N`) and `Directory.Packages.props` wiring
- [x] Local NuGet feed for development (`NuGet.config` + `dotnet nuget push` to a folder)
- [x] `templates/rayforge-game`: game library + web host + desktop host + assets + README
- [x] Template tests: create in a temp dir, build both hosts
- [x] Framework README: install, first game, deployment
- [x] UI decision record (`docs/UI-STRATEGY.md`, no ImGui dependency)

**Notes:** `RayforgeVersion` in `Directory.Build.props` is the single version source; the pack targets reject
anything that is not `0.x.y-preview.N`. The nupkgs are built into `artifacts/packages` and pushed into
`nuget-local/` by `scripts/pack.sh`. `Rayforge.Core`/`Desktop` have no package content beyond their assembly
and the shared README/LICENSE/notices; `Rayforge.Web` also carries `rayforge.js`, scoped CSS, static web assets
and `buildTransitive/Rayforge.Web.targets`.

The wasm flags are a `.targets`, not a `.props`, because the Razor SDK already generates
`buildTransitive/Rayforge.Web.props` for static web assets and NuGet imports both conventions. The template
pins `RayforgeVersion` and `RayforgeFeed` as `dotnet new` options and has no `ProjectReference` to framework
source. Packages declare their dependencies on `Raylib-cs`/`Rayforge.Core`; no native `raylib.a` is packed.

**Result:** `./scripts/pack.sh` produces `Rayforge.Core`, `Rayforge.Web` and `Rayforge.Desktop`
`0.1.0-preview.1`; each nupkg contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, and no
`runtimes/` or native archive. [`tests/template-smoke.sh`](../tests/template-smoke.sh) packs into a throwaway
feed, installs the template, generates `TemplateGame`, restores from the feed, builds the desktop and web
hosts, checks `-O0` plus the Raylib-cs `raylib.a` link, and publishes `wwwroot` with the real asset bytes.
The generated app also ran: headless Chromium loaded 1280x720 Fit, preloaded 1 asset (`motd.txt`), started the
loop with no console/page errors; the desktop host opened 1280x720 and logged `ExampleGame ready
(browser=False)`.

`docs/UI-STRATEGY.md` records the ImGui probe (works with `ImGui.NET.SourceBuild`, but no renderer/input
backend, variadic WASM trap, +~2.31 MB raw at `-O0`) and keeps ImGui out of v1.

**Open:** packages are not published to nuget.org; the template defaults to `../nuget-local` and docs say how
to point it elsewhere. Package IDs were checked free on nuget.org on 2026-10-22 but are not reserved. Firefox
and Safari remain unverified (only Chromium installed). M5 later added CI, deploy, Playwright and documented
the `-O0` decision.

**Closed:** 2026-10-22.

---

## M5 - CI and deployment

**Goal:** a green pipeline that publishes the demo and catches blank-canvas regressions.
**Exit criteria:** clean-runner build + test + publish + deploy, with a smoke test that would fail on a black canvas.

- [x] Fix the Release `wasm-opt` failure (spike 0005) or make the workaround official
- [x] `ci.yml`: setup-dotnet, `wasm-tools`, restore, build Release, test, publish, assert key output files
- [x] `deploy-hello-world.yml`: GitHub Pages, `.nojekyll`, `404.html`, sub-path safe `<base href="./">`
- [x] Playwright smoke test: page loads, canvas is non-blank, one frame advances, no console errors
- [x] AOT spike (0006): measure build time, payload, runtime; decide opt-in policy
- [x] Size/time report in CI (raw + brotli) with a regression threshold
- [x] Document the deploy recipe for any static host

**Notes:** the Release link keeps `-O0` as the official setting. The latest .NET 10 workload (10.0.113,
Emscripten 3.1.56) still pairs with Binaryen 117, which rejects `enable-bulk-memory-opt`; a newer external
Binaryen links but the post-link `wasm-opt` step fails validation, and skipping it yields a non-booting app
([spike 0005](spikes/0005-release-wasm-opt.md)). The defaults live in
`src/Rayforge.Web/buildTransitive/Rayforge.Web.targets`, so package consumers get the same behaviour.

AOT is opt-in. `mono-aot-cross` crashes precompiling Raylib-cs, so the framework marks that assembly
`AOT_InternalForceToInterpret` (the SDK's own escape hatch for huge assemblies). With that, AOT builds and
passes the browser smoke test, but costs ~5x build time, 18.58 MB raw / 4.02 MB brotli versus 6.63 MB /
2.03 MB, and does not improve startup (669 ms vs 612 ms median). It stays off by default
([spike 0006](spikes/0006-aot.md)).

**Result:** `ci.yml` is `build -> smoke -> deploy`. Build publishes HelloWorld, asserts the real assets and
native wasm, checks the payload budget against `tests/web-size-baseline.env`, and uploads the artifact. Smoke
downloads that artifact, serves it under `/Rayforge/`, and boots it in headless Chromium: start button, no
overlay, 1280x720 Fit canvas, non-blank colors, motion between two frames, no console/page errors, no failed
requests. A blank or frozen canvas fails the job. Deploy is a reusable workflow that runs only on `main`
after both jobs pass, adds `.nojekyll` and a redirecting `404.html`, and publishes the tested artifact via
GitHub Pages. Pages is enabled with the Actions source.

The payload measured from a clean publish is 6,633,481 raw bytes / 2,032,185 brotli bytes / 55 files /
2,491,188 native wasm bytes. CI reports publish seconds and the size table in the job summary; the size gate
has ~10% headroom.

**Open:** Firefox/Safari remain unverified (only Chromium installed). Packages are still local-only; the
public NuGet IDs are not reserved. AOT has no CI job by design. The `-O2` path needs a future .NET workload
with Binaryen 118+ before the default can change. The `404.html` redirect hardcodes the repository name, so a
custom domain needs a one-line change in the workflow.

**Closed:** 2026-10-23.

---

## M6 - Wolfrender port (separate track)

**Goal:** port the large real game; stress-test the base.
**Exit criteria:** Wolfrender playable in the browser with no Wolfrender-specific code in the framework.

- [ ] Split Wolfrender into a `Wolfrender.Game` library with no Blazor/JS references (still on its current raylib)
- [ ] Desktop host against the new library
- [ ] Adapt Wolfrender to the 8.1.0 / raylib 6.0 API (the base baseline since M0b); record the diff
- [ ] Replace `BrowserPointerLockBridge` / `GraphicsFramePacing` with `Context.MouseCapture` / host pacing
- [ ] Assets: 5 MB set, manifests/groups, preload progress
- [ ] Shaders: GLSL ES variants
- [ ] Editor, ImGui and the highscore server excluded from the web build (decide their future)
- [ ] Record the porting effort and update `PORTING-GUIDE.md`

---

## Backlog (not scheduled)

- HiDPI mode (`FillHiDpi`) with backing store at DPR and a documented UI-scaling contract
- Integer scaling option for pixel-art games
- Preload groups with a progress bar component
- Multi-game gallery/samples host (one game per page, not simultaneous)
- Gamepad and touch input
- Save data via browser storage / downloads
- Keep raylib current: run [UPGRADING-RAYLIB.md](UPGRADING-RAYLIB.md) when a release fixes something we need
  (not on a schedule)

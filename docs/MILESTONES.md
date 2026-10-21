# Milestones

Living checklist. Tick items as they are completed; add the date and the evidence link when a milestone closes.
The roadmap rationale lives in [PLAN.md](PLAN.md) section 9; this file is the tracker.

Legend: `[x]` done, `[ ]` planned, **(blocked)** needs something external.

## Status overview

| # | Milestone | Scope | Status |
|---|---|---|---|
| M0 | Spike | Prove Raylib-cs 7.0.2 + `raylib.a` in a fresh Blazor app | **Done** - 2026-10-21 |
| M1 | Skeleton | Solution, framework projects, contracts, asset plumbing, CI build | Not started |
| M2 | Hello World | Base canvas + JS bridge + desktop runner + demo game on both hosts | Not started |
| M3 | First real port | DoomFire-Raylib on web and desktop; porting guide validated | Not started |
| M4 | Template & packages | `dotnet new` template, packable framework, local feed | Not started |
| M5 | CI & deploy | GitHub Actions, Pages deploy, Playwright smoke test, size report | Not started |
| M6 | Wolfrender port | Separate track: port Wolfrender onto the base | Not started |
| M7 | Raylib upgrade | Raylib-cs 8.1.0 / raylib 6.0, drop the vendored `raylib.a` if it holds | Not started |

M0 was the decision gate and is closed. M1 is the next action.

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

---

## M1 - Skeleton

**Goal:** a buildable, empty framework with hard boundaries and no game logic.
**Exit criteria:** `dotnet build` is green, dependency rules are documented and enforced by a test, the web host
boots to a Razor loading screen and fails gracefully.

- [ ] Create `Rayforge.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`
- [ ] `src/Rayforge.Core`: `IGame`, `GameBase`, `GameContext`, `GameOptions`, `CanvasScaleMode`, `AssetMode`,
      `IMouseCapture`, `IRuntimeHost`, `IGameLog`, `GameDiagnostics` - no Blazor, no JavaScript interop
- [ ] `src/Rayforge.Web`: Razor Class Library skeleton, `wwwroot/rayforge.js`, scoped CSS, DI entry point
- [ ] `src/Rayforge.Desktop`: `DesktopRunner` + desktop services
- [ ] `games/HelloWorld/HelloWorld.Game`: empty `IGame` implementation that compiles against `Rayforge.Core`
- [ ] `games/HelloWorld/HelloWorld.Web` + `HelloWorld.Desktop`: hosts that boot and show "loading"/"no game" states
- [ ] Asset plumbing: `Assets.props` + the `wwwroot/assets` sync target from spike 0001, plus a build-generated
      asset list for the web host (no hand-maintained array)
- [ ] Framework `buildTransitive/Rayforge.Web.props` with `WasmBuildNative`, `WasmAllowUndefinedSymbols`,
      `EmccLinkOptimizationFlag=-O0` (documented as temporary), and the `NativeFileReference` for `raylib.a`
- [ ] Architecture test: `.Game` references no Blazor/JS assembly; `Core` references no web assembly
- [ ] `tests/Rayforge.Tests` project with the first unit tests for `CanvasLayout`
- [ ] `.github/workflows/ci.yml`: restore, build, test (no deploy yet)
- [ ] Document the dependency rules and the "one raylib context per page" rule

**Notes:** `wwwroot/assets/` is generated output and stays gitignored. `raylib.a` lives once, in the framework
project, not per game.

---

## M2 - Hello World

**Goal:** the full pipeline with a real game on both hosts.
**Exit criteria:** every acceptance criterion in [HELLO-WORLD.md](HELLO-WORLD.md) passes.

- [ ] `RayforgeCanvas` component: `OnAfterRenderAsync` boot, loading/error overlays, error boundary, teardown
- [ ] `rayforge.js`: canvas binding, rAF loop, resize, fullscreen, asset preload into MEMFS, visibility pause
- [ ] `RayforgeBridge`: synchronous `[JSExport]` frame entry, crash capture, stop-on-error
- [ ] `CanvasLayout`: pure Fit/Fill math, unit-tested (CSS size, mouse scale, letterbox offsets)
- [ ] `BrowserMouseCapture`: pointer lock, `movementX/Y` accumulation, scale applied
- [ ] `BrowserRuntimeHost`: fullscreen, quit, `document.title`
- [ ] `Rayforge.Desktop`: runner, cursor capture, resize polling, teardown
- [ ] Hello World game: cube, text, logo texture, font, beep, rings, diagnostics, resize, fullscreen
- [ ] Generated assets: `logo.png`, `blip.ogg`, `ui.ttf` with license notes in `Assets/README.md`
- [ ] Diagnostics overlay (F3/D) fed by host counters, never per-frame Blazor renders
- [ ] Pointer-lock, fullscreen and audio verified in Chrome and Firefox
- [ ] Record publish size, load time and memory in `HELLO-WORLD.md`

---

## M3 - First real port: DoomFire-Raylib

**Goal:** prove the porting guide on a game the framework did not design for.
**Exit criteria:** DoomFire runs on web and desktop from one `net10.0` game library; friction list written down.

- [ ] Extract DoomFire into `games/DoomFire/DoomFire.Game` (no Blazor, no window/loop)
- [ ] Shaders: GLSL ES variant selected at runtime, not with `#if BROWSER`
- [ ] Web + desktop hosts, assets under `Assets/`, `Assets.props`
- [ ] Preload/manifest mode (groups + progress) if asset volume justifies it
- [ ] Update `PORTING-GUIDE.md` with every real friction point
- [ ] Confirm the "under ~50 lines of host-specific changes" expectation (or explain why not)
- [ ] Pointer-lock spike (0003) if any input needs it; otherwise defer to M6

---

## M4 - Template and packages

**Goal:** a new game can be created without touching the framework source.
**Exit criteria:** `dotnet new rayforge-game` produces a project that builds and runs against packed packages.

- [ ] `IsPackable=true` for `Rayforge.Core`, `Rayforge.Web`, `Rayforge.Desktop`; pack `buildTransitive` props,
      `raylib.a`, README and LICENSE
- [ ] Versioning scheme (`0.x-preview.N`) and `Directory.Packages.props` wiring
- [ ] Local NuGet feed for development (`nuget.config` + `dotnet nuget push` to a folder)
- [ ] `templates/rayforge-game`: game library + web host + desktop host + assets + README
- [ ] Template tests: create in a temp dir, build both hosts
- [ ] Framework README: install, first game, deployment

---

## M5 - CI and deployment

**Goal:** a green pipeline that publishes the demo and catches blank-canvas regressions.
**Exit criteria:** clean-runner build + test + publish + deploy, with a smoke test that would fail on a black canvas.

- [ ] Fix the Release `wasm-opt` failure (spike 0004) or make the workaround official
- [ ] `ci.yml`: setup-dotnet, `wasm-tools`, restore, build Release, test, publish, assert key output files
- [ ] `deploy-hello-world.yml`: GitHub Pages, `.nojekyll`, `404.html`, sub-path safe `<base href="./">`
- [ ] Playwright smoke test: page loads, canvas is non-blank, one frame advances, no console errors
- [ ] AOT spike (0005): measure build time, payload, runtime; decide opt-in policy
- [ ] Size/time report in CI (raw + brotli) with a regression threshold
- [ ] Document the deploy recipe for any static host

---

## M6 - Wolfrender port (separate track)

**Goal:** port the large real game; stress-test the base.
**Exit criteria:** Wolfrender playable in the browser with no Wolfrender-specific code in the framework.

- [ ] Split Wolfrender into a `Wolfrender.Game` library with no Blazor/JS references (still on its current raylib)
- [ ] Desktop host against the new library
- [ ] Raylib-cs upgrade if still required (or take it from M7)
- [ ] Replace `BrowserPointerLockBridge` / `GraphicsFramePacing` with `Context.MouseCapture` / host pacing
- [ ] Assets: 5 MB set, manifests/groups, preload progress
- [ ] Shaders: GLSL ES variants
- [ ] Editor, ImGui and the highscore server excluded from the web build (decide their future)
- [ ] Record the porting effort and update `PORTING-GUIDE.md`

---

## M7 - Raylib upgrade

**Goal:** move to Raylib-cs 8.1.0 / raylib 6.0 and delete the vendored `raylib.a` if the official package holds up.
**Exit criteria:** base and Hello World green on 8.1.0, no regressions in the M2 acceptance criteria.

- [ ] Spike 0006: 8.1.0 `browser-wasm` inside the Rayforge base (native link, `buildTransitive` flags, canvas)
- [ ] Remove `raylib.a`, `Raylib.props` native bits and the ABI-drift workarounds if the package covers them
- [ ] Adapt any API changes across `Rayforge.Core/Web/Desktop` and the sample games
- [ ] Re-run the browser matrix and the publish-size measurements
- [ ] Update the version matrix in `PLAN.md`

---

## Backlog (not scheduled)

- HiDPI mode (`FillHiDpi`) with backing store at DPR and a documented UI-scaling contract
- Integer scaling option for pixel-art games
- Preload groups with a progress bar component
- Multi-game gallery/samples host (one game per page, not simultaneous)
- Gamepad and touch input
- Save data via browser storage / downloads

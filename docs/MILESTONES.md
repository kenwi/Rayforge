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
| M2 | Hello World | Base canvas + JS bridge + desktop runner + demo game on both hosts | Not started |
| M3 | First real port | DoomFire-Raylib on web and desktop; porting guide validated | Not started |
| M4 | Template & packages | `dotnet new` template, packable framework, local feed | Not started |
| M5 | CI & deploy | GitHub Actions, Pages deploy, Playwright smoke test, size report | Not started |
| M6 | Wolfrender port | Separate track: port Wolfrender onto the base | Not started |

M0 was the decision gate and is closed; M0b settled the raylib baseline and removed the former M7. M1 built the
skeleton; M2 is the next action.

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
- [x] Framework `buildTransitive/Rayforge.Web.props` with `WasmBuildNative`, `EmccLinkOptimizationFlag=-O0`
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

- [ ] `RayforgeCanvas` component: `OnAfterRenderAsync` boot, loading/error overlays, error boundary, teardown
- [ ] `rayforge.js`: canvas binding, rAF loop, resize, fullscreen, asset preload into MEMFS, visibility pause
- [ ] `RayforgeBridge`: synchronous `[JSExport]` frame entry, crash capture, stop-on-error
- [ ] `CanvasLayout`: pure Fit/Fill math, unit-tested (CSS size, mouse scale, letterbox offsets) *(math + tests landed in M1)*
- [ ] `BrowserMouseCapture`: pointer lock, `movementX/Y` accumulation, scale applied
- [ ] `BrowserRuntimeHost`: fullscreen, quit, `document.title`
- [ ] `Rayforge.Desktop`: runner, cursor capture, resize polling, teardown *(runner + services landed in M1; audio to enable)*
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
- [ ] Pointer-lock spike (0004) if any input needs it; otherwise defer to M6

---

## M4 - Template and packages

**Goal:** a new game can be created without touching the framework source.
**Exit criteria:** `dotnet new rayforge-game` produces a project that builds and runs against packed packages.

- [ ] `IsPackable=true` for `Rayforge.Core`, `Rayforge.Web`, `Rayforge.Desktop`; pack `buildTransitive` props,
      README, LICENSE and `THIRD-PARTY-NOTICES` (raylib / Raylib-cs are Zlib) - no native binary, the Raylib-cs
      dependency brings it
- [ ] Versioning scheme (`0.x-preview.N`) and `Directory.Packages.props` wiring
- [ ] Local NuGet feed for development (`nuget.config` + `dotnet nuget push` to a folder)
- [ ] `templates/rayforge-game`: game library + web host + desktop host + assets + README
- [ ] Template tests: create in a temp dir, build both hosts
- [ ] Framework README: install, first game, deployment

---

## M5 - CI and deployment

**Goal:** a green pipeline that publishes the demo and catches blank-canvas regressions.
**Exit criteria:** clean-runner build + test + publish + deploy, with a smoke test that would fail on a black canvas.

- [ ] Fix the Release `wasm-opt` failure (spike 0005) or make the workaround official
- [ ] `ci.yml`: setup-dotnet, `wasm-tools`, restore, build Release, test, publish, assert key output files
- [ ] `deploy-hello-world.yml`: GitHub Pages, `.nojekyll`, `404.html`, sub-path safe `<base href="./">`
- [ ] Playwright smoke test: page loads, canvas is non-blank, one frame advances, no console errors
- [ ] AOT spike (0006): measure build time, payload, runtime; decide opt-in policy
- [ ] Size/time report in CI (raw + brotli) with a regression threshold
- [ ] Document the deploy recipe for any static host

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

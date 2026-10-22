# UI strategy: Dear ImGui, raygui, or game-owned UI

Status: **accepted for v1** (2026-10-22)
Scope: player-facing interfaces and debug/tuning overlays in a Rayforge game.

## Decision

Rayforge **does not adopt Dear ImGui as its UI foundation**. The v1 contract stays small and predictable:

1. **Player-facing UI is game-owned and renderer-portable.** Draw it with raylib, a small in-repo UI layer, or
   a pure managed raygui port. It must work identically on web and desktop.
2. **Blazor owns only the shell UI** around the canvas: loading, click-to-play, errors, settings, menus and
   other low-frequency HTML overlays. Blazor must not render per frame or own the game HUD.
3. **Dear ImGui is not a dependency of `Rayforge.Core`, `Rayforge.Web`, `Rayforge.Desktop`, or a game
   library.** A future optional debug backend may live behind a separate `Rayforge.Ui` package (for example
   `Rayforge.Ui.ImGui`) and must never be referenced by a game's portable library.
4. **No UI code is added in M4.** This document records the investigation so the decision is not reopened
   from scratch.

The current non-goal in [PLAN.md](PLAN.md) (section 2.2) remains correct. If a real game needs immediate-mode
tooling, start with the fallback in "When to revisit" rather than adding ImGui to the core.

## Why this came up

The web host already gives us a good shell: `RayforgeCanvas` shows loading/progress, a click-to-play overlay,
fatal errors with a reload action, and it can host Blazor HTML around the canvas. That is enough for the
startup and recovery paths. It is not enough for a game HUD, inventory, console, level editor or tuning panels:
those need to be drawn in the same frame as the game, with the same input state and the same logical
resolution, on both hosts.

Dear ImGui is the obvious shortcut because it is mature, familiar, and used by the desktop-only Wolfrender
editor stack. The question was whether it can now be a **shared** renderer-backed UI for Rayforge on Blazor
WebAssembly, not merely whether it compiles there.

## What was tested

A throwaway Blazor WebAssembly probe (not committed; it lived under `/tmp/opencode/imgui-probe`) used:

| Item | Version / setup |
|---|---|
| SDK | .NET 10.0.402, `net10.0` |
| wasm-tools / Emscripten | 10.0.13 / 3.1.56 |
| Renderer | Raylib-cs 8.1.0 (`browser-wasm` archive, WebGL 1 / GLSL ES 100) |
| ImGui managed | `ImGui.NET` 1.91.6.1 |
| ImGui native source | `ImGui.NET.SourceBuild` 1.91.6 |
| Probe | build and boot, create widgets, inspect draw data; desktop console probe alongside it |

The probe established the following.

### What works

- ImGui.NET plus ImGui.NET.SourceBuild can be compiled and linked into a .NET 10 / Emscripten 3.1.56
  Blazor WebAssembly app.
- The managed `ImGui` API runs, widgets can be created, and ImGui produces draw lists after a frame.
- Pinning the managed and source-build packages to the same ImGui version (1.91.6 / 1.91.6.1) is required;
  the packages are not version-compatible across minor releases.
- A small freetype stub is enough to satisfy the native link when the freetype builder is not used:
  `ImGuiFreeType_GetBuilderForFreeType` and `SetAllocatorFunctions`.

### What does not work as a drop-in

- **No renderer or input backend is included.** ImGui produces vertex/index data and a command list; Rayforge
  would still need a raylib renderer backend (textures, shaders, scissor state) and a bridge from raylib input
  to ImGui IO on both hosts. That is framework work, not a package reference.
- **Variadic text traps on WASM.** `ImGui.Text(...)` fails with a `function signature mismatch` trap. The
  fixed-signature `ImGui.TextUnformatted(...)` works. Any game or helper that uses the variadic convenience
  API would need a port, and the failure mode is a runtime trap rather than a compile error.
- **The size is not free.** At `-O2` the probe added roughly **0.99 MB raw / 0.31 MB brotli**; at the current
  `-O0` fallback it added roughly **2.31 MB raw / 0.74 MB brotli**. This is in the same range as the rest of
  the game payload and is paid by every player, including those who never open a debug panel.
- **Source-build drift.** `ImGui.NET.SourceBuild` 1.92.8 had no matching managed NuGet package at test time.
  The framework would need a version matrix and pinning policy, and a wasm link check on every upgrade.
- The probe was desktop/Chromium-only. Firefox and Safari were not installed, so the trap and backend behavior
  are not verified there.

This is enough to say "technically possible", but not enough to say "drop-in shared UI". The missing renderer,
input backend, WASM trap surface, payload cost and version coupling all land on the framework.

## Options considered

| Option | Verdict | Why |
|---|---|---|
| **Dear ImGui as the shared UI foundation** | **Rejected for v1** | No renderer/input backend; WASM traps; +2.31 MB raw at `-O0`; version coupling; not a small core dependency. |
| **Raygui-style immediate-mode layer, pure managed** | **Recommended for player UI when needed** | Same draw calls on both hosts, no native dependency, small API, works with the existing Fit/Fill and input pipeline. |
| **Blazor HTML overlays for menus/options** | **Use for the shell, not the HUD** | Great for loading, start, errors, settings and low-frequency menus; cannot be shared with desktop and must not render per frame. |
| **Game-owned raylib UI** | **Always allowed** | No framework API needed; best for a HUD that is specific to one game. |
| **Optional ImGui debug backend** | **Allowed later, behind a package boundary** | Useful for tuning/editors; must be opt-in, desktop-first if necessary, and kept out of the game library. |

A pure managed raygui port is preferred over wrapping the C raygui library because it keeps the game library
plain `net10.0`, avoids another native archive, and still uses only raylib drawing calls. The port does not
exist yet; this is a direction, not a hidden dependency.

## Consequences

- `Rayforge.Core` stays free of UI frameworks and third-party native libraries.
- A game can still use ImGui today if it takes the dependency itself and writes its own backend; Rayforge does
  not prevent it. It will not be portable without that backend work.
- The template and docs teach raylib drawing + Blazor shell UI. They do not ship an ImGui sample.
- The `Rayforge.Web` package keeps its current payload and link behavior. No `buildTransitive` ImGui flags.
- M5's size report must include any future UI package; the `-O0` workaround makes ImGui's cost especially
  visible.

## When to revisit

Reconsider a shared `Rayforge.Ui` package only if at least one of these becomes true:

1. A real port (Wolfrender in M6 or a later game) needs an editor/debug UI that cannot reasonably be drawn
   with raygui-style code.
2. A renderer + input backend exists and has been exercised on Chromium, Firefox and Safari.
3. The `wasm-opt -O2` path is fixed (M5), so the payload measurement is representative.
4. ImGui.NET and ImGui.NET.SourceBuild publish matching, stable versions for the SDK/Emscripten baseline.

If revisited, keep the boundary:

```text
Game library -> Rayforge.Core (+ optional Rayforge.Ui facade)
Host         -> Rayforge.Web/Desktop (+ optional Rayforge.Ui.ImGui backend)
```

The game library must not reference ImGui types. A debug-only overlay may be compiled into a desktop/editor
host, but the web player build stays free of it unless the payload and browser verification justify it.

## Evidence and follow-ups

- ImGui probe: `/tmp/opencode/imgui-probe` (managed widgets + draw data; not committed). Desktop probe:
  `/tmp/opencode/imgui-desktop`. Package inspection: `/tmp/opencode/imgui/`.
- The probe's browser size figures are from the same Release `-O0` baseline used by Rayforge; remeasure after
  M5 changes the link optimization.
- The UI row in [PORTING-GUIDE.md](PORTING-GUIDE.md) still says "raylib-drawn" for ported games; that remains
  the default.
- This decision does not add a milestone. If `Rayforge.Ui` is funded, give it its own spike and exit criteria.

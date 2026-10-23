# Spike 0005 - the Release `wasm-opt` failure

- **Date:** 2026-10-22
- **Time spent:** ~45 min
- **Milestone:** M5
- **Verdict:** **FALL BACK, officially.** Keep `EmccLinkOptimizationFlag=-O0` as the supported Release link.
  The installed .NET 10 toolchain still pairs Emscripten 3.1.56 with Binaryen 117, which rejects a feature
  flag the link step emits. There is no supported, dependency-free fix yet.

## 1. Question

`dotnet publish -c Release` links at `-O2`, which runs `wasm-opt` with `--enable-bulk-memory-opt`; Binaryen
117 answers `Unknown option` and the link fails. M0 found this, M0b confirmed it was not raylib, and M1
shipped `-O0` as a temporary workaround. M5 owns the decision:

> Can the current workload/SDK link at `-O2`, or should `-O0` become the official Release setting?

## 2. Environment

| Component | Version |
|---|---|
| OS | Linux (Omarchy/Arch), x86_64 |
| .NET SDK | 10.0.402 |
| wasm-tools workload | 10.0.113/10.0.100 (latest available) |
| Emscripten | 3.1.56 (`Microsoft.NET.Runtime.Emscripten.3.1.56.Sdk.linux-x64/10.0.13`) |
| Binaryen | **117** (`tools/bin/wasm-opt --version`) |
| Repro | `dotnet publish games/HelloWorld/HelloWorld.Web -c Release -p:EmccLinkOptimizationFlag=-O2` |

`dotnet workload update --print-rollback` reports 10.0.113 for every Emscripten manifest, and
`dotnet workload list` shows no pending update. Updating the workload therefore does not change the pair.

## 3. Results

### 3.1 Baseline: `-O2` still fails

```text
wasm-opt ... --mvp-features --enable-bulk-memory --enable-bulk-memory-opt ...
Unknown option '--enable-bulk-memory-opt'
emcc : error : '.../wasm-opt ...' failed (returned 1)
```

The publish stops in `_BrowserWasmLinkDotNet`; nothing after the native link runs. This is byte-for-byte the
M0 failure, so it is toolchain-only and unaffected by Raylib-cs 8.1.0.

### 3.2 Disabling bulk memory does not help

- `EmccExtraLDFlags="-sBULK_MEMORY=0"` → `emcc : error : BULK_MEMORY is an internal setting and cannot be
  set from command line`.
- `EmccExtraLDFlags="-mno-bulk-memory"` → accepted, but the linked wasm still advertises bulk-memory
  features (raylib and the .NET runtime are compiled with them), so `wasm-opt` receives the same flag and
  fails identically.

### 3.3 A newer Binaryen is not a drop-in fix

Installing Binaryen 132 from npm and pointing `BINARYEN_ROOT`/`EM_BINARYEN_ROOT` at it lets the **emcc** link
finish, because 132 understands `--enable-bulk-memory-opt`. The SDK's separate post-link `wasm-opt` step then
fails validation:

```text
[wasm-validator error in function LoadModel] all used features should be allowed
Fatal: error validating input
```

Skipping that step with `-p:WasmRunWasmOpt=false` produces a publish, but the app never leaves
`Loading Rayforge…`: the post-link step is not optional in practice. Using an external Binaryen would also
add a machine-level dependency that the NuGet package cannot express, so it is not a framework fix.

## 4. Decision

1. **`EmccLinkOptimizationFlag=-O0` is the official Release setting**, not a temporary workaround. The
   framework targets keep the default and document why. Users can still override the property, but the
   documented path is `-O0`.
2. **Do not add a Binaryen dependency.** The failure is in the .NET Emscripten pack, and a per-machine
   override is not reproducible for package consumers.
3. **Keep the size budget** (`tests/check-web-size.sh`) so the cost of `-O0` is visible and a future native
   size regression fails CI.
4. Revisit when a .NET 10 workload ships Emscripten with Binaryen 118+ (or Microsoft patches the emitted
   feature list). The check is one command:

   ```bash
   dotnet workload list
   dotnet publish games/HelloWorld/HelloWorld.Web -c Release -p:EmccLinkOptimizationFlag=-O2
   ```

   If it links, verify with the browser smoke test before changing the default.

## 5. Follow-ups

| # | Item | Owner |
|---|---|---|
| 1 | Keep the `-O0` default and the explanation in `Rayforge.Web.targets` | M5 (done) |
| 2 | Point ARCHITECTURE, README, UPGRADING-RAYLIB and PORTING-GUIDE at this record | M5 (done) |
| 3 | Re-test after each .NET SDK/workload update; do not bump the default without a browser smoke pass | ongoing |

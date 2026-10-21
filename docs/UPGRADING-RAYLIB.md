# Upgrading raylib (and Raylib-cs)

Rayforge pins exactly **two** versions of one stack: the `Raylib-cs` NuGet package (managed bindings + the
native raylib archive for every platform). This document is the repeatable procedure for moving them, so the
next upgrade is a planned afternoon instead of a spike.

The last upgrade is recorded in [spike 0002](spikes/0002-raylib-cs-8.1.0.md); its appendix at the bottom lists
what actually bit us. Read that after the steps, not before.

---

## 0. What is pinned where

| Thing | Single source of truth | Do not |
|---|---|---|
| `Raylib-cs` version | `Directory.Packages.props` (`PackageVersion`) | Pin it in a game's `.csproj` or in two places |
| Native raylib | comes from the package (`runtimes/<rid>/native/`) | Commit a `raylib.a`, `libraylib.so` or `.dll` into the repo |
| Emscripten link flags | `Rayforge.Web/buildTransitive/Rayforge.Web.props` | Add `-sUSE_GLFW=3` yourself (the package does) |
| Docs that name the version | `PLAN.md` 6.6, `PORTING-GUIDE.md` compatibility table, `HELLO-WORLD.md` baseline, `MILESTONES.md` | Let a doc keep the old version "for history" - history lives in the spike record |

If any of those disagree after an upgrade, the upgrade is not finished.

---

## 1. Decide whether to upgrade at all

Upgrade when there is a reason:

- a bug fix or API you actually need,
- the package stops working with the current .NET SDK / Emscripten band,
- a security or licensing fix.

Do **not** upgrade in the middle of a port, right before a release, or in the same branch as a framework
refactor. An upgrade is a version bump plus whatever it drags in - nothing else.

## 2. Time-box and branch

- One branch, one pull request, one version bump.
- Time-box the probe to half a day. If it is not green by then, stop and record what failed; do not "fix it on
  the way".
- Never upgrade and change game code semantics in the same commit. API renames are mechanical; behaviour
  changes get their own commit (and their own note).

## 3. Establish the baseline *before* touching anything

You cannot judge "no regression" without numbers from the current version. On `main`, in a clean tree:

```bash
dotnet build -c Debug
dotnet publish <a-web-host> -c Release -p:EmccLinkOptimizationFlag=-O0 -o /tmp/base
```

Record:

| Metric | Command |
|---|---|
| Published size / file count | `find /tmp/base/wwwroot -type f ! -name '*.br' ! -name '*.gz' -printf '%s\n' \| awk '{s+=$1;n++} END {print s, n}'` |
| Brotli size | `find /tmp/base/wwwroot -name '*.br' -printf '%s\n' \| awk '{s+=$1} END {print s}'` |
| Native wasm size | `ls -l /tmp/base/wwwroot/_framework/dotnet.native.*.wasm` |
| Link rsp | `obj/Release/net10.0/wasm/for-publish/emcc-link.rsp` |
| Runtime | load-to-first-frame, FPS, texture/audio validity lines from the browser console |
| Warnings | `dotnet build` output must be grep-clean for `undefined symbol` |

Keep the folder. It is the A/B half of every later comparison.

## 4. Check the target version exists and read its release notes

```bash
curl -s https://api.nuget.org/v3-flatcontainer/raylib-cs/index.json | python3 -m json.tool
```

Then inspect the package **before** changing code:

```bash
PKG=~/.nuget/packages/raylib-cs/<version>
cat  $PKG/*.nuspec | grep -E 'version|license'
find $PKG/runtimes -name 'raylib.*' -o -name 'libraylib.*' -o -name '*.dylib'
cat  $PKG/buildTransitive/*.targets        # native reference + any EmccFlags it adds
ls   $PKG/lib                                # TFMs it supports
```

Answer three questions from that alone:

1. Does it still ship `runtimes/browser-wasm/native/raylib.a`? If not, the web path needs a different plan.
2. Which raylib release is the archive built from? (`TargetRaylibTag` in the targets; the desktop SONAME,
   e.g. `libraylib.so.600` = raylib 6.0, is a good cross-check.)
3. Does it target our TFM, and did the license change?

## 5. Bump one version

```bash
# Directory.Packages.props
<PackageVersion Include="Raylib-cs" Version="<new>" />
```

Nothing else. No props edits, no flags, no workarounds yet. You want the build to tell you what changed, not
to hide it behind a compat shim.

## 6. Reconcile the props file

Read `buildTransitive/Raylib-cs.targets` and compare it with `Rayforge.Web.props`. For each flag we set, decide
**keep / delete / let the package do it**:

| Ours | Verdict rule |
|---|---|
| `NativeFileReference` to `raylib.a` | delete as soon as the package ships one - never link two archives |
| `-sUSE_GLFW=3` | delete if the package adds it for `browser-wasm` |
| `-sALLOW_MEMORY_GROWTH`, `-sTOTAL_MEMORY` | delete unless a measured failure says otherwise (see §7) |
| `-sFORCE_FILESYSTEM` | delete if `Module.FS` exists and assets preload without it |
| `-DPLATFORM_WEB` | keep only if the code actually `#if`s on it; prefer `Context.IsBrowser` |
| `WasmAllowUndefinedSymbols` | **delete**. Undefined symbols must fail the build, not trap at runtime |
| `WasmBuildNative` | usually keep until verified that the package sets it |
| `EmccLinkOptimizationFlag=-O0` | temporary toolchain workaround, not a raylib concern - see §9 |

Verify the result in the generated response file, not by faith:

```bash
grep -oE 'raylib[^ ]*\.a|USE_GLFW=[0-9]|ALLOW_MEMORY_GROWTH[^ ]*' \
  obj/Release/net10.0/wasm/for-publish/emcc-link.rsp | sort | uniq -c
```

Expected: exactly one archive, exactly one `USE_GLFW`, no flag you did not decide to keep.

## 7. ABI gate: the link must be silent

```bash
dotnet build -c Debug 2>&1 | grep -E 'undefined symbol|warning CS|error' || echo CLEAN
```

- **Any `undefined symbol:` line is a failure**, even though the app may still boot. The bindings call entry
  points that are not in the archive; the call traps only when that code path runs - usually in a game, months
  later.
- Do not "solve" it with `WasmAllowUndefinedSymbols=true`. Either take the archive the bindings were built
  against (the package's), or rebuild the archive from the matching commit.

Then check the managed surface actually changed where the release notes said it did. Quick audit of the XML
docs the package ships:

```bash
X=~/.nuget/packages/raylib-cs/<version>/lib/net10.0/Raylib-cs.xml
grep -oE 'M:Raylib_cs\.Raylib\.[A-Za-z]+' $X | sed 's/.*\.//' | sort -u > /tmp/new-api.txt
# diff against the old version's list to see removed/renamed entry points
```

Anything removed that the framework or a game calls must be adapted **now**, in this PR - not discovered in M3.

## 8. Compile the whole surface, not the demo

```bash
dotnet build -c Debug                    # everything in the solution
dotnet build -c Release                  # catches trimming/optimisation-only issues
```

`Release` also runs the native link at `-O2`; if it fails in `wasm-opt` (§9), re-run with `-O0` before you
blame raylib.

## 9. Do not confuse the toolchain with the library

Two failures look like upgrade failures and are not:

| Symptom | Real cause | Action |
|---|---|---|
| `wasm-opt: Unknown option '--enable-bulk-memory-opt'` at `-O2` | Emscripten pack ships a Binaryen older than the flag the SDK passes | keep `EmccLinkOptimizationFlag=-O0`; solve in M5; **it is not a raylib problem** |
| A CRLF/trailing-slash path failure in `NativeFileReference` | MSBuild path normalisation on Windows | use `$(MSBuildThisFileDirectory)` with forward slashes, test on Windows |

Confirm it is the toolchain by running the **old** version through the same failing command. If it fails
identically, the upgrade is innocent.

## 10. Run the probe harness before writing docs

Use the M0-style probe (canvas, cube, text, texture, sound, resize, HUD) as the fixed test rig. It must pass
**all** of these, on both the DevServer and a static publish:

| Check | Pass condition |
|---|---|
| Boot | `window ready=True`, `audio ready=True`, no page error, no black canvas |
| Assets | `File.Exists` true, texture `valid=True`, sound `valid=True` |
| Frames | HUD FPS > 55 in headless SwiftShader; click produces a ring and a beep |
| Input | held click registers exactly once; sub-frame synthetic click is dropped (expected) |
| Fit mode | 900x506 canvas, mouse marker exact at 25%/50%, `scale 1.42` |
| HiDPI | DPR 2: CSS size unchanged, mouse exact at 25%/75% |
| Shader/audio paths | if the game uses them, run it - the probe does not cover shaders |

Memory and file system sanity, if you removed flags:

```js
// in the page console: the heap must be able to grow past the initial size
const m = Blazor.runtime.Module, before = m.HEAPU8.length;
const p = m._malloc(40 * 1024 * 1024); m._free(p);
console.log(before / 1048576, m.HEAPU8.length / 1048576);   // e.g. 32 -> 70.8
```

## 11. Measure and compare

Run the same publish as the baseline (§3) and compare:

| Metric | Accept when |
|---|---|
| Raw / brotli size | within a few percent; a regression needs a reason in the record |
| File count | unchanged, or explained (fingerprint churn does not count) |
| `dotnet.native.wasm` | shrink is normal (native archive size), growth needs a note |
| Load to first frame | within noise on localhost |
| FPS | no sustained drop |

Investigate any delta you cannot explain. "It got smaller" is a fine outcome, but you should know why.

## 12. Desktop, then record, then revert-or-keep

1. **Desktop:** `dotnet run --project <desktop-host>`. The package ships the native library for the build RID;
   if it does not load, that is the upgrade's real cost and belongs in the record. Linux is the only RID we can
   test locally - mark the others untested in the doc.
2. **Cross-browser:** Chrome is the local baseline; Firefox/Safari need a manual pass. Do not claim them.
3. **Write the record** (spike doc or the upgrade section of the PR):
   - target version + the native raylib version it carries,
   - what was removed from our props (and why),
   - the ABI result (undefined symbols before/after),
   - the measurement table,
   - every API removed/renamed and what replaced it,
   - the unresolved issues, with the milestone that owns them.
4. **Update the docs that name versions:** `PLAN.md` 6.6, `PORTING-GUIDE.md`, `HELLO-WORLD.md`,
   `MILESTONES.md`, `docs/spikes/README.md`.
5. **Keep or revert.** Keep only if all gates in §7, §8, §10, §12.1 pass. Otherwise revert the single version
   bump - because nothing else was changed, the revert is one line and the probe record still has value.

---

## Appendix - what bit us in 0002 (7.0.2 -> 8.1.0)

Real order of events, so the next upgrade anticipates them:

1. **Moved the upgrade earlier than planned.** M7 was parked after M6, but M1 has no rendering at all, so a
   broken native link would have stayed invisible until M2. A probe before M1 was cheaper than debugging in M2.
   *Lesson:* do native/toolchain changes before the first milestone that has no way to observe them.
2. **The package did all the link work.** No `Raylib.props`, no `NativeFileReference`, exactly one
   `-sUSE_GLFW=3` in the rsp. Don't fight it with a second archive.
3. **Three undefined symbols disappeared.** The 6.0 bindings no longer reference `GetSoundsPlaying`,
   `DrawLineBezierCubic` or `DrawLineBezierQuad` at all. Dropping `WasmAllowUndefinedSymbols` turned "silent
   runtime trap" into "build error", which is the behaviour we want.
4. **`-O2` still failed.** Same `wasm-opt --enable-bulk-memory-opt` error; not raylib, not the upgrade. We
   tested the old version through the same command to prove it.
5. **Our custom Emscripten flags were dead weight.** `ALLOW_MEMORY_GROWTH` is on by default (heap 32 -> 70.8 MB
   on a 40 MB malloc), `FORCE_FILESYSTEM` is unnecessary (MEMFS exists), `TOTAL_MEMORY` is obsolete. Deleting
   all of them removed a whole class of "why is this flag here" questions.
6. **The size went down**, which is not the usual upgrade outcome: the archive shrank by 268 KB, the bindings
   assembly grew by 37 KB, net -178 KB raw / -26 KB brotli.
7. **Check the exact helper names.** The validity helpers are `IsTextureValid`, `IsSoundValid`, `IsShaderValid`,
   etc.; there is no `IsTextureReady`. Read the shipped XML docs instead of guessing. Seven public entry points
   were removed between 7.0.2 and 8.1.0 (`GetSoundsPlaying`, `DrawModelPoints*`, `UnloadModelAnimation`,
   `UpdateModelAnimationBones`, ...); a port that uses them needs a mechanical rename, not a runtime bug hunt.
8. **The asset copy target was untouched.** It is Blazor static-web-asset behaviour (0001, 4.2), so an upgrade
   must not "clean it up".
9. **The package is Zlib-licensed.** If the project is MIT, the notices still have to mention raylib/raylib-cs.

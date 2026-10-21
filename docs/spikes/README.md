# Spikes

Short, time-boxed experiments that answer one risky question. A spike is allowed to be throwaway code; its
result must be written down here so the decision survives.

## Rules

- One question per spike; time-box it (usually half a day).
- Record the environment (SDK, workload, browser, OS) and the exact commands.
- Record what worked, what failed, the error text and the workaround.
- End with a decision: proceed, change the plan, or fall back.
- Do not merge spike code into `src/` without a review; rewrite it properly.

## Template

```markdown
# Spike 000X - <question>

- Date:
- Time spent:
- Environment: .NET SDK, wasm-tools, OS, browser

## Question
What exactly are we trying to prove?

## Setup
Commands, project type, package versions, relevant MSBuild properties.

## Results
What worked. Include logs/screenshots/sizes where useful.

## Failures
Errors, stack traces, things that did not work.

## Decision
Proceed / change plan / fall back. What changes in PLAN.md?

## Follow-ups
```

## Completed spikes

| ID | Question | Milestone | Result |
|---|---|---|---|
| [0001](0001-raylib-cs-blazor-wasm.md) | Does Raylib-cs 7.0.2 + a vendored `raylib.a` work inside a fresh Blazor WebAssembly app (canvas binding, native link, `[JSExport]` loop, assets, audio, resize)? | M0 | **GO**, with 4 plan corrections |
| [0002](0002-raylib-cs-8.1.0.md) | Does Raylib-cs 8.1.0's official `browser-wasm` package replace the vendored `raylib.a` and `Raylib.props` cleanly (no undefined symbols, no regressions, smaller payload)? | M0b | **GO.** M1 starts on 8.1.0; raylib 6.0; 6.04 MB / 1.85 MB brotli; M7 closed |
| [0003](0003-doomfire-port.md) | Can DoomFire-Raylib port onto Rayforge with mechanical changes; does the porting guide hold? | M3 | **GO.** Under ~50 lines of host-specific game changes; MIME + GLSL ES 100 friction recorded |

## Planned spikes

| ID | Question | Milestone |
|---|---|---|
| 0004 | Pointer lock + mouse delta under `Blazor.runtime.Module['canvas']`; does raylib's `DisableCursor` help at all? | M6 |
| 0005 | Release publish: the `wasm-opt --enable-bulk-memory-opt` failure (see 0001 section 4.3) - workload update, SDK band, or patched response file? | M5 |
| 0006 | AOT in Release: build time, payload size, runtime stability with the package's raylib archive. | M5 |

Fit/Fill canvas math was validated as part of M1/M2 unit tests and Hello World acceptance; it no longer needs a
separate spike ID.

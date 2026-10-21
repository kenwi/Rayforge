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

## Planned spikes

| ID | Question | Milestone |
|---|---|---|
| 0002 | Fit/Fill canvas math: correct mouse mapping at DPR 1/1.25/1.5/2 and browser zoom, in Chrome and Firefox? | M2 |
| 0003 | Pointer lock + mouse delta under `Blazor.runtime.Module['canvas']`; does raylib's `DisableCursor` help at all? | M3 |
| 0004 | Release publish: the `wasm-opt --enable-bulk-memory-opt` failure (see 0001 section 4.3) - workload update, SDK band, or patched response file? | M5 |
| 0005 | AOT in Release: build time, payload size, runtime stability with the vendored raylib. | M5 |
| 0006 | Raylib-cs 8.1.0 (raylib 6.0, official `browser-wasm` package) inside the Rayforge base: does it replace the vendored `raylib.a` cleanly? | M7 |

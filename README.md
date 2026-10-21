# Rayforge

A small framework for running C# / [Raylib](https://raylib.com) games in the browser with Blazor WebAssembly,
while keeping the game code itself portable to desktop.

> **Status: M0 + M0b done, M1 next.** The spike proved the stack end to end (3D + 2D + texture + audio +
> letterboxed mouse mapping, on the DevServer and from a static publish), and the upgrade probe moved the
> baseline to Raylib-cs 8.1.0 / raylib 6.0 with no vendored native binary. No framework code exists yet.
> Roadmap and checklists: [docs/MILESTONES.md](docs/MILESTONES.md).

## Why

`Wolfrender.Blazor.Raylib` proved that a Blazor canvas + a statically linked `raylib.a` can drive a complete
game in the browser. It also mixed the browser bridge, the host and one specific game into a single project.
Rayforge is a clean-room extraction of the *idea*: a reusable base, a game library, and thin hosts. No
Wolfrender source code is copied.

The intended end state:

```
game code (net10.0, Raylib-cs only)  ->  same game on desktop and in the browser
```

## Decisions

| | |
|---|---|
| Runtime | .NET 10, Blazor WebAssembly (standalone) |
| Renderer | raylib 6.0 via Raylib-cs 8.1.0; the native archive (browser + desktop RIDs) comes from the package |
| Loop | `requestAnimationFrame` -> one synchronous `[JSExport]` per frame |
| Canvas | Fit (fixed logical resolution + `SetMouseScale`) and Fill, both supported |
| Assets | Fetched into the Emscripten MEMFS at boot (Blazor does not support `WasmFilesToIncludeInFileSystem`) |
| Shape | `src/Rayforge.{Core,Web,Desktop}` + `games/<Game>/{.Game,.Web,.Desktop}` |
| License | MIT |

## Documents

| Document | Contents |
|---|---|
| [docs/PLAN.md](docs/PLAN.md) | Architecture, alternatives, build and packaging, risks, recorded decisions |
| [docs/MILESTONES.md](docs/MILESTONES.md) | All milestones M0-M6 with checklists |
| [docs/UPGRADING-RAYLIB.md](docs/UPGRADING-RAYLIB.md) | How to upgrade Raylib-cs / native raylib without a spike |
| [docs/spikes/](docs/spikes/) | Spike records (0001 stack proof, 0002 raylib 8.1.0 upgrade) |
| [docs/HELLO-WORLD.md](docs/HELLO-WORLD.md) | The first demo: behavior and acceptance criteria |
| [docs/PORTING-GUIDE.md](docs/PORTING-GUIDE.md) | Checklist for bringing an existing Raylib-cs game to the web |
| [docs/spikes/](docs/spikes/) | Time-boxed experiments and their results |

## Spike (M0)

The proof of concept lives in [`spike/m0-raylib-blazor/`](spike/m0-raylib-blazor) and is throwaway code, kept
as evidence. See [spike 0001](docs/spikes/0001-raylib-cs-blazor-wasm.md) for findings, measurements and the
four plan corrections it produced.

```bash
dotnet workload install wasm-tools                                          # once
dotnet run --project spike/m0-raylib-blazor                                 # dev server
dotnet publish spike/m0-raylib-blazor -c Release -p:EmccLinkOptimizationFlag=-O0 -o artifacts/m0
```

Serve `artifacts/m0/wwwroot` with any server that sends `application/wasm`.

## Acknowledgements

Rayforge stands on the shoulders of:

- [Raylib](https://raylib.com) (zlib license)
- [Raylib-cs](https://github.com/raylib-cs/raylib-cs) (MIT), including its official browser-wasm support
- [Blazor.Raylib](https://github.com/trucidare/Blazor.Raylib) (MIT) and [DotnetRaylibWasm](https://github.com/stanoddly/DotnetRaylibWasm)
- `Wolfrender.Blazor.Raylib` (local sibling project) for proving the approach

## License

MIT - see [LICENSE](LICENSE).

# Rayforge

A small framework for running C# / [Raylib](https://raylib.com) games in the browser with Blazor WebAssembly,
while keeping the game code itself portable to desktop.

> **Status: M4 done (template and packages).** The same `net10.0` game library runs in the browser and in a
> native window. `dotnet new rayforge-game` consumes packed `Rayforge.Core`, `Rayforge.Web` and
> `Rayforge.Desktop` packages, with no project reference back into the framework. Hello World and DoomFire
> remain the in-repo demos. Roadmap and checklists: [docs/MILESTONES.md](docs/MILESTONES.md).

## Why

`Wolfrender.Blazor.Raylib` proved that a Blazor canvas + a statically linked `raylib.a` can drive a complete
game in the browser. It also mixed the browser bridge, the host and one specific game into a single project.
Rayforge is a clean-room extraction of the *idea*: a reusable base, a game library, and thin hosts. No
Wolfrender source code is copied.

The intended end state:

```text
game code (net10.0, Raylib-cs only)  ->  same game on desktop and in the browser
```

## Create a game

Requirements: .NET SDK 10.0.x, the `wasm-tools` workload, and a display for the desktop host.

```bash
dotnet workload install wasm-tools
git clone https://github.com/kenwi/Rayforge.git
cd Rayforge

./scripts/pack.sh                                      # packages into ./nuget-local
dotnet new install ./templates/rayforge-game
dotnet new rayforge-game -n MyGame --RayforgeFeed "$PWD/nuget-local"

cd MyGame
dotnet run --project MyGame.Web                         # browser host
dotnet run --project MyGame.Desktop                     # native window
```

The generated tree is `MyGame.Game` (portable library), `MyGame.Web` (Blazor host), `MyGame.Desktop`
(console host), and `Assets/` (shared source assets). The `Game` project may use only `Rayforge.Core` and
`Raylib-cs`; the hosts contain no game logic. Use `GameContext.IsBrowser` for platform differences; never
`#if BROWSER`.

`--RayforgeFeed` can point at any folder or NuGet feed. The template's default is `../nuget-local`; pass
`--RayforgeVersion` when packing a different preview. Once the packages are published, the generated
`NuGet.config` can point at nuget.org instead.

## Deploy the web host

```bash
dotnet publish MyGame.Web -c Release -o artifacts/my-game
```

Serve `artifacts/my-game/wwwroot` with any static server that sends `application/wasm`, including under a
sub-path. The generated `<base href="./" />` and the framework's `buildTransitive` wasm flags are already in
place. The current Release link uses `-O0` (Binaryen 117 workaround, owned by M5); do not override it by hand.

## Work on the framework

```bash
dotnet build Rayforge.slnx
dotnet test Rayforge.slnx
dotnet run --project games/HelloWorld/HelloWorld.Web
dotnet run --project games/HelloWorld/HelloWorld.Desktop
dotnet run --project games/DoomFire/DoomFire.Web
dotnet run --project games/DoomFire/DoomFire.Desktop
./tests/template-smoke.sh                               # pack -> template -> build both hosts -> publish
```

The in-repo games use project references for a fast inner loop. The packages and template use the local feed,
which is the consumer path. Package metadata, README/LICENSE/notices, and the `buildTransitive` wasm targets
are part of every `.nupkg`; no native raylib binary is packed. Raylib-cs supplies it.

## Decisions

| | |
|---|---|
| Runtime | .NET 10, Blazor WebAssembly (standalone) |
| Renderer | raylib 6.0 via Raylib-cs 8.1.0; the native archive (browser + desktop RIDs) comes from the package |
| Loop | `requestAnimationFrame` -> one synchronous `[JSExport]` per frame |
| Canvas | Fit (fixed logical resolution + `SetMouseScale`) and Fill, both supported |
| Assets | Fetched into the Emscripten MEMFS at boot (Blazor does not support `WasmFilesToIncludeInFileSystem`) |
| Shape | `src/Rayforge.{Core,Web,Desktop}` + `games/<Game>/{.Game,.Web,.Desktop}` + `templates/rayforge-game` |
| Packages | `Rayforge.*` `0.1.0-preview.1`; local folder feed in `nuget-local/`; Raylib-cs stays a dependency |
| UI | Game-owned / raylib-drawn; Dear ImGui is not a framework dependency ([docs/UI-STRATEGY.md](docs/UI-STRATEGY.md)) |
| License | MIT |

## Documents

| Document | Contents |
|---|---|
| [docs/PLAN.md](docs/PLAN.md) | Architecture, alternatives, build and packaging, risks, recorded decisions |
| [docs/MILESTONES.md](docs/MILESTONES.md) | All milestones M0-M6 with checklists |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Project graph, dependency rules, one-context rule, boot sequences, asset pipeline |
| [docs/PACKAGE-README.md](docs/PACKAGE-README.md) | The README shipped inside each NuGet package |
| [docs/UI-STRATEGY.md](docs/UI-STRATEGY.md) | ImGui investigation and the UI decision record |
| [docs/UPGRADING-RAYLIB.md](docs/UPGRADING-RAYLIB.md) | How to upgrade Raylib-cs / native raylib without a spike |
| [docs/spikes/](docs/spikes/) | Spike records (0001 stack proof, 0002 raylib upgrade, 0003 DoomFire port) |
| [docs/HELLO-WORLD.md](docs/HELLO-WORLD.md) | The first demo: behavior and acceptance criteria |
| [docs/PORTING-GUIDE.md](docs/PORTING-GUIDE.md) | Checklist for bringing an existing Raylib-cs game to the web |

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
- [Raylib-cs](https://github.com/raylib-cs/raylib-cs) (zlib license), including its official browser-wasm support
- [Blazor.Raylib](https://github.com/trucidare/Blazor.Raylib) (MIT) and [DotnetRaylibWasm](https://github.com/stanoddly/DotnetRaylibWasm)
- `Wolfrender.Blazor.Raylib` (local sibling project) for proving the approach

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the license texts.

## License

MIT - see [LICENSE](LICENSE).

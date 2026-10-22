# RayforgeGame

A portable C# / [Raylib](https://raylib.com) game. The same `RayforgeGame.Game` project runs in a
Blazor WebAssembly page and in a native desktop window.

Generated with `dotnet new rayforge-game`. The folder names and namespaces follow the name you passed with
`-n`.

## Prerequisites

- .NET SDK 10.0.x
- `wasm-tools` for the web host: `dotnet workload install wasm-tools`
- A display for the desktop host

The project consumes **Rayforge packages**, not framework project references. `NuGet.config` points at the
feed passed with `--RayforgeFeed`; the default is `../nuget-local`. The template option names are
case-sensitive (`--RayforgeFeed`, `--RayforgeVersion`).

```bash
# From the Rayforge checkout (once):
./scripts/pack.sh
```

## Run

```bash
dotnet restore RayforgeGame.slnx
dotnet run --project RayforgeGame.Web
dotnet run --project RayforgeGame.Desktop
```

The web host opens on the URL printed by the DevServer. The desktop host opens a 1280x720 window.

## Structure

```text
RayforgeGame.Game/       net10.0 library: game rules, assets, raylib drawing
RayforgeGame.Web/        Blazor WebAssembly host: GameOptions + AddRayforge
RayforgeGame.Desktop/    console host: GameOptions + DesktopRunner
Assets/                  shared source assets; copied by Assets.props
Assets.props             asset plumbing imported by both hosts
```

`RayforgeGame.Game` may use `Rayforge.Core` and `Raylib-cs`. It must not use Blazor, JS interop,
`Rayforge.Web` or `Rayforge.Desktop`. Use `Context.IsBrowser` for platform differences; do not add
`#if BROWSER`.

## First edits

1. Rename `ExampleGame` and the `RayforgeGame` namespace with your game's name.
2. Update `GameOptions` in both host `Program.cs` files (title, logical size, Fit/Fill, audio).
3. Add assets under `Assets/` and load them with lowercase `assets/...` paths.
4. Draw the game in `ExampleGame.Draw`; keep `BeginDrawing`/`EndDrawing` there.
5. Use `Update(dt)` for time; never add a blocking loop or `SetTargetFPS` on web.

## Deploy

```bash
dotnet publish RayforgeGame.Web -c Release -o artifacts/rayforge-game
```

Serve `artifacts/rayforge-game/wwwroot` with a static server that sends `application/wasm`. The generated
`<base href="./" />` works under a sub-path. Do not add wasm flags by hand: the `Rayforge.Web` package
ships them through `buildTransitive`.

## Documentation

- [Architecture](https://github.com/kenwi/Rayforge/blob/main/docs/ARCHITECTURE.md)
- [Porting guide](https://github.com/kenwi/Rayforge/blob/main/docs/PORTING-GUIDE.md)
- [UI strategy](https://github.com/kenwi/Rayforge/blob/main/docs/UI-STRATEGY.md)

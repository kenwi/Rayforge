# Rayforge packages

Rayforge runs one `net10.0` C# / [Raylib](https://raylib.com) game library in the browser and in a native
window. The framework packages are small on purpose:

| Package | Role |
|---|---|
| `Rayforge.Core` | Portable contracts: `IGame`, `GameBase`, `GameContext`, `GameOptions`, host services. No Blazor, no JS interop. |
| `Rayforge.Web` | Blazor WebAssembly host: canvas component, JS bridge, asset preload, WebGL boot sequence. |
| `Rayforge.Desktop` | Native raylib window, frame loop, cursor capture and teardown. |

A game library depends on `Rayforge.Core` (plus `Raylib-cs`). A web host depends on `Rayforge.Web`; a desktop
host depends on `Rayforge.Desktop`. The native raylib archive for both hosts comes from the `Raylib-cs`
dependency - these packages contain no native binary.

## Requirements

- .NET SDK 10.0.x
- `wasm-tools` workload for the web host: `dotnet workload install wasm-tools`
- Raylib-cs 8.1.0 (restored as a dependency; it brings raylib 6.0 natives)

## Start a game

The framework repository ships a template. From a checkout of the repository:

```bash
./scripts/pack.sh
dotnet new install ./templates/rayforge-game
dotnet new rayforge-game -n MyGame --RayforgeFeed "$PWD/nuget-local"
cd MyGame
dotnet run --project MyGame.Web
dotnet run --project MyGame.Desktop      # needs a display
```

When the packages are published, omit `--RayforgeFeed` and point the generated `NuGet.config` at nuget.org.
The generated project uses normal `PackageReference`s and an ordinary `Directory.Packages.props`; there is no
project reference back into the framework source.

## Project shape

```text
MyGame/
  MyGame.Game/       net10.0, Rayforge.Core + Raylib-cs: game rules, assets, drawing
  MyGame.Web/        Blazor WebAssembly host: only GameOptions + registration
  MyGame.Desktop/    console host: only GameOptions + DesktopRunner
  Assets/            shared source assets; copied by Assets.props into both hosts
```

Keep game code free of `#if BROWSER`. Use `GameContext.IsBrowser` when a shader or capability differs, and
`GameContext.Runtime` / `MouseCapture` for window and pointer operations. One raylib context per page.

## Deploy the web host

```bash
dotnet publish MyGame.Web -c Release -o artifacts/my-game
```

Serve `artifacts/my-game/wwwroot` from any static host that sends `application/wasm`. Keep the generated
`<base href="./" />` if the app lives under a sub-path. The framework's `buildTransitive` targets are applied
automatically; do not add the wasm flags by hand.

## Documentation

- [Architecture and dependency rules](https://github.com/kenwi/Rayforge/blob/main/docs/ARCHITECTURE.md)
- [Porting guide](https://github.com/kenwi/Rayforge/blob/main/docs/PORTING-GUIDE.md)
- [Milestones](https://github.com/kenwi/Rayforge/blob/main/docs/MILESTONES.md)
- [UI strategy](https://github.com/kenwi/Rayforge/blob/main/docs/UI-STRATEGY.md)

## License

MIT. See `LICENSE` and `THIRD-PARTY-NOTICES.md` in the package. Raylib and Raylib-cs are Zlib-licensed; the
.NET/Blazor packages are MIT.

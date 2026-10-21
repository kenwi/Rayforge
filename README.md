# Rayharbor

**Working name.** A small framework for running C# / [Raylib](https://raylib.com) games in the browser with
Blazor WebAssembly, while keeping the game code itself portable to desktop.

> **Status: planning only.** No implementation exists yet. The architecture, alternatives and open questions
> are in [docs/PLAN.md](docs/PLAN.md). The first implementation step is a technical spike (M0) to validate the
> current Raylib-cs browser-wasm support inside a Blazor app.

## Why

`Wolfrender.Blazor.Raylib` proved that a Blazor canvas + a statically linked `raylib.a` can drive a complete
game in the browser. It also mixed the browser bridge, the host and one specific game into a single project.
Rayharbor is a clean-room extraction of the *idea*: a reusable base, a game library, and thin hosts.

The intended end state:

```
game code (net10.0, Raylib-cs only)  ->  same game on desktop and in the browser
```

## Documents

| Document | Contents |
|---|---|
| [docs/PLAN.md](docs/PLAN.md) | Full plan: findings, naming, project-structure alternatives, architecture, build, roadmap, risks, decisions needed |
| [docs/HELLO-WORLD.md](docs/HELLO-WORLD.md) | The first demo: exact behaviour and acceptance criteria |
| [docs/PORTING-GUIDE.md](docs/PORTING-GUIDE.md) | Draft checklist for bringing an existing Raylib-cs game to the web |

## Proposed shape (not created yet)

```
src/Rayharbor.Core/          portable contracts (IGame, GameContext) - no Blazor
src/Rayharbor.Web/           Blazor canvas component, JS bridge, browser services
src/Rayharbor.Desktop/       optional desktop runner
games/HelloWorld/            .Game + .Web + .Desktop
```

## Acknowledgements

Rayharbor stands on the shoulders of:

- [Raylib](https://raylib.com) (zlib licence)
- [Raylib-cs](https://github.com/raylib-cs/raylib-cs) (MIT), including its official browser-wasm support
- [Blazor.Raylib](https://github.com/trucidare/Blazor.Raylib) (MIT) and [DotnetRaylibWasm](https://github.com/stanoddly/DotnetRaylibWasm)
- [Wolfrender.Blazor.Raylib](https://github.com/) for proving the approach

No Wolfrender source code is copied into this project.

## Licence

Not chosen yet. MIT is the working assumption; see the open questions in the plan.

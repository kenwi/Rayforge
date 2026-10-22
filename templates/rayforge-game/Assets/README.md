# Rayforge template assets

Put every runtime file the game loads here. Both hosts copy this folder, and the web host generates
`assets/rayforge.assets.json` from it at build time.

- Use lowercase, relative paths in code: `assets/player.png`, `assets/shaders/...`.
- Do not load from `wwwroot` or an absolute path; the same path must work in the browser MEMFS and on desktop.
- Text-like shader assets should use a content type the Blazor DevServer serves. `.txt` is safe;
  `.frag` / `.vert` / `.glsl` return 404 on the DevServer.
- Update `Assets.props` only for exclusions (for example documentation or license folders).

`motd.txt` is loaded by `ExampleGame.OnInitialize` to prove the asset path.

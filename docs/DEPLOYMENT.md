# Deployment

The web host is a static site: `dotnet publish` produces `wwwroot` with HTML, JS, `.wasm` assemblies, the
Blazor framework files and the game assets. There is no server-side component. This document covers the
GitHub Pages pipeline used by the demo and the requirements for any other static host.

## What CI does

[`ci.yml`](../.github/workflows/ci.yml) has three jobs:

```text
build -> smoke -> deploy (main only)
```

1. **build** — restore, Release build, unit tests, publish HelloWorld, assert the asset manifest and native
   wasm, check the payload budget, upload `hello-world-web`.
2. **smoke** — download the artifact, install Playwright, serve it under `/Rayforge/`, then boot the game in
   headless Chromium. It fails on a blank canvas, a frozen frame, console errors or 4xx/5xx requests.
3. **deploy** — a reusable workflow in
   [`deploy-hello-world.yml`](../.github/workflows/deploy-hello-world.yml). It runs only on a push to `main`,
   downloads the *tested* artifact, adds `.nojekyll` and `404.html`, and deploys to GitHub Pages.

Pull requests never deploy. A failed build or smoke test leaves the previous Pages deployment in place.

## GitHub Pages setup

The repository's Pages source must be **GitHub Actions**:

1. Settings → Pages → Build and deployment → Source: **GitHub Actions**.
2. Push to `main`, or run the `ci` workflow manually.
3. The deployed URL appears in the `deploy` job and in the `github-pages` environment.

The workflow calls `actions/configure-pages@v5` with `enablement: true`, so it can create the Pages site on
the first run when the token is allowed to. If the organization disables that, enable Pages once in the
settings and re-run the job.

For this repository the site is <https://kenwi.github.io/Rayforge/>.

### Sub-path safety

The host's `<base href="./" />` is intentional. GitHub Pages serves project sites under `/<repo>/`, and every
script, stylesheet, `_framework` URL and asset in the app is relative to that base. Do not replace it with
`/` unless the app is served from a domain root.

### `.nojekyll` and `404.html`

GitHub Pages runs Jekyll unless a `.nojekyll` file is present. The deploy workflow writes one before
uploading; without it, files and folders whose names begin with `_` (including `_framework/`) can be
ignored.

Pages has no SPA fallback. The workflow writes a small `404.html` that redirects to `/<repo>/`, because a
copy of `index.html` would resolve `./` against the missing path and fail to load `_framework`. If you use a
custom domain or a different repository name, update the redirect target in the workflow.

## Build and run locally

```bash
dotnet workload install wasm-tools                 # once
dotnet publish games/HelloWorld/HelloWorld.Web -c Release -o artifacts/hello-world

# Serve it the way Pages does: under a sub-path.
mkdir -p artifacts/site/Rayforge
cp -a artifacts/hello-world/wwwroot/. artifacts/site/Rayforge/
cd artifacts/site && python3 -m http.server 8080
# open http://127.0.0.1:8080/Rayforge/
```

Any static server is fine if it sends `application/wasm` for `.wasm`. Python's `http.server` does on
current versions; older versions may send `application/octet-stream` and the runtime rejects the module.

### Browser smoke test

```bash
cd tests/web-smoke
npm ci
npx playwright install --with-deps chromium      # once; or set CHROMIUM_PATH to an installed Chromium
npx playwright test
```

The default site root is `artifacts/site/Rayforge`. Override it with `SMOKE_ROOT=/path/to/wwwroot` and the
port with `SMOKE_PORT`. The test waits for the start button, clicks it, checks the 1280x720 canvas, compares
two frames, and fails on console errors or failed requests.

To point Playwright at a system browser instead of its own download:

```bash
CHROMIUM_PATH=/usr/bin/chromium npx playwright test
```

## Payload budget

```bash
./tests/check-web-size.sh artifacts/hello-world/wwwroot
```

It prints raw bytes (excluding `.br`/`.gz` siblings), brotli bytes, file count and the native wasm size,
then compares them with [`tests/web-size-baseline.env`](../tests/web-size-baseline.env). CI runs the same
script and fails on a regression. When a change intentionally grows the payload, update the limits and the
comment in that file with the reason.

The Release link uses `-O0` because the current .NET 10 Emscripten pack ships Binaryen 117, which rejects a
feature flag that `-O2` passes to `wasm-opt`. This is the official supported setting until the toolchain is
fixed; see [spike 0005](spikes/0005-release-wasm-opt.md). Do not override it to `-O2` without verifying a
real browser publish.

## Optional AOT

AOT is not enabled in CI or by default. To measure or ship it deliberately:

```bash
dotnet publish games/HelloWorld/HelloWorld.Web -c Release \
  -p:RunAOTCompilation=true -o artifacts/hello-world-aot
```

The framework's `buildTransitive` targets keep Raylib-cs interpreted, because `mono-aot-cross` crashes while
precompiling that assembly on the current toolchain. Measured on HelloWorld: ~1.5 minutes versus ~16 seconds,
18.6 MB raw / 4.0 MB brotli versus 6.6 MB / 2.0 MB, and no startup improvement (612 ms → 669 ms). See
[spike 0006](spikes/0006-aot.md) for the full numbers. If you enable AOT for a game, run the browser smoke
test against that publish too.

## Other static hosts

The publish output is host-agnostic. Requirements:

| Requirement | Notes |
|---|---|
| Serve `.wasm` as `application/wasm` | Required by the streaming WebAssembly API. |
| Keep the folder layout | `wwwroot/index.html`, `_framework/`, `assets/`. |
| Relative base | Keep `<base href="./" />`; works at a domain root or under a sub-path. |
| Compression | Serve the `.br` siblings with `Content-Encoding: br`, or compress on the fly. Never set the header on the raw file. |
| Caching | Hashed `_framework` files and assets can be immutable; `index.html`, `blazor.webassembly.js` and the manifest should be revalidated. |
| No SPA rewrite needed | There is one page. A catch-all rewrite is harmless but unnecessary. |

Examples:

- **Netlify / Cloudflare Pages / Vercel** — publish directory `artifacts/<game>/wwwroot`; the build command is
  the `dotnet publish` above. These hosts set `application/wasm` and compress automatically.
- **Amazon S3 + CloudFront** — upload `wwwroot`, set `application/wasm` on `*.wasm`, set `index.html` as the
  index document and a small redirect page as the error document, and invalidate `index.html` on deploy.
- **nginx** — `types { application/wasm wasm; }`, `try_files $uri $uri/ =404;`, and enable `brotli` or
  `gzip` for `application/wasm`, JS and JSON.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Page stays on `Loading Rayforge…` | `_framework` files missing (Jekyll ignored `_framework` without `.nojekyll`), wrong base path, or `.wasm` served with the wrong MIME type. |
| Works at `/` but not under `/Rayforge/` | `<base href="/">` or absolute asset URLs; restore `<base href="./" />`. |
| Deploy job fails at `configure-pages` | Pages is not enabled and the token cannot enable it; set Source to GitHub Actions in Settings → Pages. |
| Deploy job fails at `deploy-pages` | The `github-pages` environment or Pages permissions are restricted; check Settings → Environments and the workflow's `pages: write` / `id-token: write`. |
| Blank canvas but the page boots | Run the smoke test; it compares frames and reports console errors. Check WebGL availability and the asset manifest. |
| 404 on a deep link | Expected: Pages serves the generated redirect page, which returns to the app root. |

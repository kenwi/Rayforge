// M0 spike: bind the Blazor/Emscripten canvas and drive one synchronous [JSExport] per frame.

let canvas = null;
let exportsApi = null;
let handle = null;
let running = false;
let raf = 0;
let last = 0;

function getModule() {
    const rt = globalThis.Blazor?.runtime;
    if (rt?.Module) return rt.Module;
    const dyn = globalThis.getDotnetRuntime?.(0);
    return dyn?.Module ?? null;
}

async function resolveRuntime() {
    const br = globalThis.Blazor?.runtime;
    if (br && typeof br.getAssemblyExports === "function") return br;

    const dyn = globalThis.getDotnetRuntime?.(0);
    if (!dyn) throw new Error("dotnet runtime is not available");
    return typeof dyn.then === "function" ? await dyn : dyn;
}

export function init(canvasId) {
    canvas = document.getElementById(canvasId);
    if (!canvas) throw new Error("canvas not found: " + canvasId);

    const mod = getModule();
    if (!mod) throw new Error("Emscripten module is not available");

    mod["canvas"] = canvas;
    console.log("[m0] module.canvas =", mod.canvas?.id, "size", canvas.width + "x" + canvas.height);

    canvas.tabIndex = 0;
    canvas.addEventListener("contextmenu", (e) => e.preventDefault());
    canvas.addEventListener("mousedown", () => canvas.focus());
    canvas.addEventListener("keydown", (e) => {
        if (["Space", "Tab", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(e.code)) {
            e.preventDefault();
        }
    });

    window.addEventListener("resize", layout);
    layout();
}

// Fit mode: backing buffer stays 1280x720, CSS size is letterboxed.
export function layout() {
    if (!canvas) return;

    const scale = Math.min(window.innerWidth / canvas.width, window.innerHeight / canvas.height);
    const cssW = Math.max(1, Math.floor(canvas.width * scale));
    const cssH = Math.max(1, Math.floor(canvas.height * scale));

    canvas.style.width = cssW + "px";
    canvas.style.height = cssH + "px";

    if (exportsApi) {
        exportsApi.M0.M0Canvas.OnCanvasResize(handle, canvas.width, canvas.height, cssW, cssH);
    }
}

// Blazor WASM ignores WasmFilesToIncludeInFileSystem, so the VFS starts empty:
// every asset has to be fetched and written into MEMFS before raylib can open it.
export async function preload(paths) {
    const mod = getModule();
    const FS = mod?.FS;
    if (!FS) throw new Error("Emscripten FS is not available");

    let bytes = 0;
    for (const path of paths) {
        const url = new URL(path, document.baseURI).href;
        const res = await fetch(url, { cache: "no-cache" });
        if (!res.ok) throw new Error(`asset ${path} -> HTTP ${res.status}`);

        const data = new Uint8Array(await res.arrayBuffer());
        bytes += data.length;

        const parts = path.split("/");
        let dir = "";
        for (let i = 0; i < parts.length - 1; i++) {
            dir += (i > 0 ? "/" : "") + parts[i];
            try { FS.mkdir(dir); } catch { /* already exists */ }
        }
        FS.writeFile(path, data);
        console.log(`[m0] preloaded ${path} (${data.length} bytes) from ${url}`);
    }
    console.log(`[m0] preload done: ${paths.length} files, ${bytes} bytes`);
}

export async function start(handleObj, assemblyName) {
    handle = handleObj;

    const rt = await resolveRuntime();
    const dll = assemblyName.endsWith(".dll") ? assemblyName : assemblyName + ".dll";
    exportsApi = await rt.getAssemblyExports(dll);

    const api = exportsApi.M0?.M0Canvas;
    console.log("[m0] exports resolved:", dll, api ? Object.keys(api) : "(none)");
    if (!api) throw new Error("M0.M0Canvas exports not found in " + dll);

    layout();

    running = true;
    last = performance.now();
    raf = requestAnimationFrame(frame);
    console.log("[m0] rAF loop started");
}

function frame(now) {
    if (!running) return;
    raf = requestAnimationFrame(frame);

    const dt = Math.min((now - last) / 1000, 0.1);
    last = now;

    try {
        if (!exportsApi.M0.M0Canvas.Frame(handle, dt)) {
            running = false;
            cancelAnimationFrame(raf);
            console.warn("[m0] loop stopped by C#");
        }
    } catch (e) {
        running = false;
        cancelAnimationFrame(raf);
        console.error("[m0] frame threw:", e);
    }
}

export function stop() {
    running = false;
    if (raf) cancelAnimationFrame(raf);
    raf = 0;
}

export function toggleFullscreen(canvasId) {
    const el = document.getElementById(canvasId)?.parentElement ?? canvas;
    if (!document.fullscreenElement) {
        return el?.requestFullscreen?.().catch((e) => console.warn("[m0] fullscreen failed:", e));
    }
    return document.exitFullscreen();
}

export function reload() {
    location.reload();
}

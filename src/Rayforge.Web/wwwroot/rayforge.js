// Rayforge web runtime. Imported by RayforgeCanvas through JSHost.ImportAsync.
//
// Responsibilities:
//   - bind the canvas to the .NET WASM module (one raylib context per page)
//   - fetch assets into the Emscripten MEMFS before the game asks for them
//   - drive the frame loop: requestAnimationFrame -> one synchronous [JSExport] per frame
//   - own browser-only input: pointer lock, fullscreen, visibility, context loss
//   - report layout changes to C#, which owns all canvas math (CanvasLayout)
//
// The C# side never paces frames, never looks at the DOM and never calls raylib from a timer.

const state = {
    canvasId: null,
    canvas: null,
    host: null,
    module: null,
    bridge: null,
    handle: null,
    running: false,
    raf: 0,
    last: 0,
    targetFps: 0,
    accumulator: 0,
    paused: false,
    observer: null,
    pointerDelta: { x: 0, y: 0 },
    cleanups: [],
};

function requireCanvas() {
    if (!state.canvas) {
        throw new Error("[rayforge] canvas is not initialized");
    }
    return state.canvas;
}

function on(target, type, handler, options) {
    target.addEventListener(type, handler, options);
    state.cleanups.push(() => target.removeEventListener(type, handler, options));
}

function getModule() {
    if (state.module) return state.module;
    const rt = globalThis.Blazor?.runtime;
    if (rt?.Module) return rt.Module;
    const dyn = globalThis.getDotnetRuntime?.(0);
    return dyn?.Module ?? null;
}

async function resolveExports(assemblyName) {
    const dll = assemblyName.endsWith(".dll") ? assemblyName : `${assemblyName}.dll`;
    const rt = globalThis.Blazor?.runtime;
    if (rt && typeof rt.getAssemblyExports === "function") {
        const exports = await rt.getAssemblyExports(dll);
        return exports?.Rayforge?.Web?.Interop?.RayforgeBridge ?? null;
    }

    const dyn = globalThis.getDotnetRuntime?.(0);
    if (!dyn) throw new Error("dotnet runtime is not available");
    const runtime = typeof dyn.then === "function" ? await dyn : dyn;
    const exports = await runtime.getAssemblyExports(dll);
    return exports?.Rayforge?.Web?.Interop?.RayforgeBridge ?? null;
}

function isFullscreen() {
    return state.host !== null && document.fullscreenElement === state.host;
}

function layoutChanged() {
    if (!state.bridge || !state.canvas || !state.host) return;
    // C# owns CanvasLayout: it decides backing size, CSS size and mouse scale.
    state.bridge.OnLayoutChanged(state.handle, state.host.clientWidth, state.host.clientHeight);
}

function setPaused(paused) {
    state.paused = paused;
    state.last = performance.now();
    state.accumulator = 0;
}

/**
 * Binds a canvas and installs the browser listeners.
 * @param {string} canvasId
 * @param {string} mode "fit" | "fill"
 * @param {string} title document.title
 * @returns {{ cssWidth: number, cssHeight: number, dpr: number }}
 */
export function init(canvasId, mode, title) {
    if (state.canvas) {
        throw new Error("[rayforge] only one canvas can be initialized per page");
    }

    const canvas = document.getElementById(canvasId);
    if (!(canvas instanceof HTMLCanvasElement)) {
        throw new Error(`[rayforge] <canvas id="${canvasId}"> not found`);
    }

    const host = canvas.parentElement ?? canvas;
    const mod = getModule();
    if (!mod) {
        throw new Error("[rayforge] Emscripten module is not available");
    }

    // raylib draws into the module canvas. Without this, InitWindow creates its own element.
    mod.canvas = canvas;

    state.canvasId = canvasId;
    state.canvas = canvas;
    state.host = host;
    state.module = mod;

    document.title = title;

    canvas.tabIndex = 0;
    canvas.style.touchAction = "none";

    on(canvas, "contextmenu", (event) => event.preventDefault());
    on(canvas, "pointerdown", () => canvas.focus());
    on(canvas, "keydown", (event) => {
        // Keep the page from scrolling while the game has focus.
        if (["Space", "Tab", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(event.code)) {
            event.preventDefault();
        }
    });

    // Layout: notify C# on any host size change. The observer also fires for fullscreen.
    state.observer = new ResizeObserver(layoutChanged);
    state.observer.observe(host);

    on(document, "visibilitychange", () => setPaused(document.hidden));

    on(document, "fullscreenchange", () => {
        state.bridge?.OnFullscreenChanged(state.handle, isFullscreen());
        layoutChanged();
    });

    on(canvas, "webglcontextlost", (event) => {
        event.preventDefault();
        stopLoop();
        state.bridge?.OnContextLost(state.handle, "WebGL context lost");
    });

    on(document, "pointerlockchange", () => {
        const captured = document.pointerLockElement === canvas;
        if (!captured) {
            state.pointerDelta.x = 0;
            state.pointerDelta.y = 0;
        }
        state.bridge?.OnPointerLockChanged(state.handle, captured);
    });

    on(document, "pointermove", (event) => {
        if (document.pointerLockElement !== canvas) return;
        state.pointerDelta.x += event.movementX || 0;
        state.pointerDelta.y += event.movementY || 0;
    });

    const dpr = window.devicePixelRatio || 1;
    console.info(
        `[rayforge] canvas "${canvasId}" bound (mode ${mode}, css ${host.clientWidth}x${host.clientHeight}, dpr ${dpr})`);

    return {
        cssWidth: host.clientWidth,
        cssHeight: host.clientHeight,
        dpr,
    };
}

/**
 * Fetches every path and writes it into the Emscripten MEMFS. raylib's loaders are synchronous,
 * so this must finish before the game loads a texture, font or sound.
 * @param {string[]} paths paths relative to the document base, e.g. "assets/logo.png"
 * @returns {Promise<number>} total bytes written
 */
export async function preload(paths) {
    const FS = getModule()?.FS;
    if (!FS) {
        throw new Error("[rayforge] Emscripten FS is not available");
    }

    let bytes = 0;
    const started = performance.now();

    for (const path of paths) {
        if (!path || path.startsWith("/") || path.includes("..")) {
            throw new Error(`[rayforge] invalid asset path: ${path}`);
        }

        const url = new URL(path, document.baseURI).href;
        const response = await fetch(url, { cache: "no-cache" });
        if (!response.ok) {
            throw new Error(`[rayforge] asset ${path} -> HTTP ${response.status} (${url})`);
        }

        const data = new Uint8Array(await response.arrayBuffer());
        bytes += data.length;

        const parts = path.split("/");
        let dir = "";
        for (let i = 0; i < parts.length - 1; i++) {
            dir += (i > 0 ? "/" : "") + parts[i];
            try {
                FS.mkdir(dir);
            } catch {
                // already exists
            }
        }

        FS.writeFile(path, data);
    }

    console.info(`[rayforge] preloaded ${paths.length} asset(s), ${bytes} bytes in ${Math.round(performance.now() - started)} ms`);
    return bytes;
}

/**
 * Resolves the C# exports and starts the rAF loop.
 * @param {object} handle opaque C# handle (the RayforgeCanvas instance)
 * @param {string} assemblyName assembly containing RayforgeBridge
 * @param {number} targetFps 0 = display refresh
 */
export async function start(handle, assemblyName, targetFps) {
    state.handle = handle;
    state.targetFps = Math.max(0, targetFps | 0);

    const bridge = await resolveExports(assemblyName);
    if (!bridge) {
        throw new Error(`[rayforge] Rayforge.Web.Interop.RayforgeBridge not found in ${assemblyName}`);
    }
    if (typeof bridge.UpdateFrame !== "function") {
        throw new Error("[rayforge] UpdateFrame export is missing (trimmer?)");
    }

    state.bridge = bridge;

    if (state.running) return;

    state.running = true;
    state.last = performance.now();
    state.accumulator = 0;
    state.raf = requestAnimationFrame(frame);
    console.info(`[rayforge] loop started (targetFps ${state.targetFps || "rAF"})`);
}

function frame(now) {
    if (!state.running) return;
    state.raf = requestAnimationFrame(frame);

    if (state.paused) {
        state.last = now;
        return;
    }

    let deltaTime = (now - state.last) / 1000;
    state.last = now;

    if (!Number.isFinite(deltaTime) || deltaTime < 0) {
        deltaTime = 0;
    }
    deltaTime = Math.min(deltaTime, 0.1);

    if (state.targetFps > 0) {
        state.accumulator += deltaTime;
        if (state.accumulator < 1 / state.targetFps) {
            return;
        }
        deltaTime = Math.min(state.accumulator, 0.1);
        state.accumulator = 0;
    }

    try {
        if (!state.bridge.UpdateFrame(state.handle, deltaTime)) {
            stopLoop();
            console.info("[rayforge] loop stopped by C#");
        }
    } catch (error) {
        stopLoop();
        console.error("[rayforge] frame threw:", error);
        state.bridge?.OnContextLost(state.handle, `frame threw: ${error?.message ?? error}`);
    }
}

function stopLoop() {
    state.running = false;
    if (state.raf) {
        cancelAnimationFrame(state.raf);
        state.raf = 0;
    }
}

/** Stops the loop. Safe to call repeatedly. */
export function stop() {
    stopLoop();
}

/** Sets the letterboxed CSS size. C# owns the math; JS only applies it. */
export function setCanvasCssSize(cssWidth, cssHeight) {
    const canvas = requireCanvas();
    canvas.style.width = `${cssWidth}px`;
    canvas.style.height = `${cssHeight}px`;
}

/** Sets the backing store size. Used by Fill mode; Fit keeps the logical size. */
export function setBackingSize(width, height) {
    const canvas = requireCanvas();
    canvas.width = Math.max(1, Math.round(width));
    canvas.height = Math.max(1, Math.round(height));
}

/** Toggles DOM fullscreen on the canvas container. Needs a user gesture. */
export async function toggleFullscreen() {
    const element = state.host ?? state.canvas?.parentElement ?? state.canvas;
    if (!element) return false;

    if (document.fullscreenElement) {
        await document.exitFullscreen();
        return false;
    }

    await element.requestFullscreen();
    return true;
}

/** Pointer lock. Must be called from a user gesture; the browser rejects it otherwise. */
export function requestPointerLock() {
    const canvas = requireCanvas();
    const result = canvas.requestPointerLock?.();
    if (result?.catch) {
        result.catch((error) => console.warn("[rayforge] pointer lock rejected:", error?.message ?? error));
    }
}

/** Releases pointer lock. */
export function releasePointerLock() {
    if (document.pointerLockElement) {
        document.exitPointerLock?.();
    }
}

/** Returns accumulated pointer-lock movement and clears it. */
export function consumePointerDelta() {
    const delta = { x: state.pointerDelta.x, y: state.pointerDelta.y };
    state.pointerDelta.x = 0;
    state.pointerDelta.y = 0;
    return delta;
}

/** Reloads the page. The only safe way to get a fresh raylib context. */
export function reload() {
    location.reload();
}

/** Stops everything and releases the canvas binding. */
export function dispose() {
    stopLoop();

    for (const cleanup of state.cleanups) {
        try {
            cleanup();
        } catch {
            // ignore
        }
    }
    state.cleanups = [];

    state.observer?.disconnect();
    state.observer = null;

    const mod = getModule();
    if (mod && mod.canvas === state.canvas) {
        mod.canvas = undefined;
    }

    if (state.canvasId) {
        console.info(`[rayforge] canvas "${state.canvasId}" released`);
    }

    state.canvasId = null;
    state.canvas = null;
    state.host = null;
    state.bridge = null;
    state.handle = null;
    state.module = null;
}

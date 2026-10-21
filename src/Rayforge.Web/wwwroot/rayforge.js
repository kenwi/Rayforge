// Rayforge web runtime. Imported by RayforgeCanvas through JSHost.ImportAsync.
//
// M1 scope: bind the canvas to the .NET WASM module and expose the measurement / sizing calls the
// component needs. The frame loop, pointer lock and asset preload land in M2 - keep the exported
// surface additive so those changes do not touch C# call sites.

/** @type {Map<string, { canvas: HTMLCanvasElement, host: HTMLElement }>} */
const canvases = new Map();

function record(canvasId) {
    const found = canvases.get(canvasId);
    if (!found) {
        throw new Error(`[rayforge] canvas #${canvasId} is not initialized`);
    }
    return found;
}

function blockContextMenu(event) {
    event.preventDefault();
}

/**
 * Binds a canvas to the .NET WASM module and reports the space available for it.
 * @param {string} canvasId
 * @returns {{ cssWidth: number, cssHeight: number, dpr: number }}
 */
export function init(canvasId) {
    if (canvases.has(canvasId)) {
        throw new Error(`[rayforge] canvas #${canvasId} is already initialized`);
    }

    const canvas = document.getElementById(canvasId);
    if (!(canvas instanceof HTMLCanvasElement)) {
        throw new Error(`[rayforge] <canvas id="${canvasId}"> not found`);
    }

    const host = canvas.parentElement ?? canvas;

    // raylib/emscripten draws into the module canvas; without this, InitWindow creates its own
    // off-screen element and nothing appears.
    const dotnetModule = globalThis.Blazor?.runtime?.Module;
    if (dotnetModule) {
        dotnetModule.canvas = canvas;
    } else {
        console.warn("[rayforge] Blazor.runtime.Module not available; raylib will bind its own canvas");
    }

    // Keyboard focus: raylib reads key events from the canvas.
    canvas.tabIndex = 0;
    canvas.addEventListener("contextmenu", blockContextMenu);

    canvases.set(canvasId, { canvas, host });

    const dpr = window.devicePixelRatio || 1;
    console.info(
        `[rayforge] canvas "${canvasId}" bound ` +
        `(backing ${canvas.width}x${canvas.height}, css ${host.clientWidth}x${host.clientHeight}, dpr ${dpr})`);

    return {
        cssWidth: host.clientWidth,
        cssHeight: host.clientHeight,
        dpr,
    };
}

/**
 * Sets the canvas CSS size. The backing store (canvas.width/height) is owned by C#.
 * @param {string} canvasId
 * @param {number} cssWidth
 * @param {number} cssHeight
 */
export function setCanvasCssSize(canvasId, cssWidth, cssHeight) {
    const { canvas } = record(canvasId);
    canvas.style.width = `${cssWidth}px`;
    canvas.style.height = `${cssHeight}px`;
}

/**
 * Releases the canvas. Safe to call after init failed.
 * @param {string} canvasId
 */
export function dispose(canvasId) {
    const found = canvases.get(canvasId);
    if (!found) {
        return;
    }

    found.canvas.removeEventListener("contextmenu", blockContextMenu);

    const dotnetModule = globalThis.Blazor?.runtime?.Module;
    if (dotnetModule && dotnetModule.canvas === found.canvas) {
        dotnetModule.canvas = undefined;
    }

    canvases.delete(canvasId);
    console.info(`[rayforge] canvas "${canvasId}" released`);
}

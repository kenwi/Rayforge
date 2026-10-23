const { test, expect } = require("@playwright/test");
const { PNG } = require("pngjs");

// The canvas is 1280x720 in Fit mode at this viewport, and the game background is #0c0e16.
const BACKGROUND = [0x0c, 0x0e, 0x16];

function analyze(image) {
  const pixels = image.width * image.height;
  const colors = new Set();
  let nonBackground = 0;
  let luminanceSum = 0;
  let luminanceSquaredSum = 0;

  for (let i = 0; i < image.data.length; i += 4) {
    const r = image.data[i];
    const g = image.data[i + 1];
    const b = image.data[i + 2];
    colors.add((r << 16) | (g << 8) | b);

    if (Math.abs(r - BACKGROUND[0]) + Math.abs(g - BACKGROUND[1]) + Math.abs(b - BACKGROUND[2]) > 12) {
      nonBackground++;
    }

    const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
    luminanceSum += luminance;
    luminanceSquaredSum += luminance * luminance;
  }

  const mean = luminanceSum / pixels;
  const variance = Math.max(0, luminanceSquaredSum / pixels - mean * mean);

  return {
    pixels,
    colors: colors.size,
    nonBackgroundRatio: nonBackground / pixels,
    luminanceStdDev: Math.sqrt(variance),
  };
}

function changedPixels(a, b) {
  if (a.width !== b.width || a.height !== b.height) {
    throw new Error(`screenshot size changed: ${a.width}x${a.height} -> ${b.width}x${b.height}`);
  }

  let changed = 0;
  for (let i = 0; i < a.data.length; i += 4) {
    if (
      Math.abs(a.data[i] - b.data[i]) +
        Math.abs(a.data[i + 1] - b.data[i + 1]) +
        Math.abs(a.data[i + 2] - b.data[i + 2]) >
      8
    ) {
      changed++;
    }
  }
  return changed;
}

test("Hello World renders an animated canvas without browser errors", async ({ page }) => {
  const errors = [];
  const failedRequests = [];

  page.on("console", (message) => {
    if (message.type() === "error") {
      errors.push(`console: ${message.text()}`);
    }
  });
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  page.on("requestfailed", (request) => {
    failedRequests.push(`${request.failure()?.errorText ?? "failed"} ${request.url()}`);
  });
  page.on("response", (response) => {
    if (response.status() >= 400) {
      failedRequests.push(`${response.status()} ${response.url()}`);
    }
  });

  // The app is served under /Rayforge/ on purpose: this is the GitHub Pages sub-path shape.
  await page.goto("/Rayforge/");

  const start = page.getByRole("button", { name: "Click to play" });
  await expect(start).toBeVisible({ timeout: 60_000 });
  await start.click();

  // BootState.Running removes the Razor overlay; the canvas keeps running underneath it.
  await expect(page.locator(".rayforge-overlay")).toHaveCount(0, { timeout: 30_000 });

  const canvas = page.locator("canvas");
  await expect(canvas).toBeVisible();
  await expect(canvas).toHaveAttribute("width", "1280");
  await expect(canvas).toHaveAttribute("height", "720");

  // Let the loop settle, then prove the cube moved between two captures. A black or frozen
  // canvas (the regression this test exists for) fails both the color and the motion checks.
  await page.waitForTimeout(1500);
  const first = PNG.sync.read(await canvas.screenshot());
  await page.waitForTimeout(600);
  const second = PNG.sync.read(await canvas.screenshot());

  const stats = analyze(first);
  expect(stats.pixels).toBe(1280 * 720);
  expect(stats.colors, "canvas has too few distinct colors; it is probably blank").toBeGreaterThan(32);
  expect(stats.nonBackgroundRatio, "canvas is almost entirely background").toBeGreaterThan(0.05);
  expect(stats.luminanceStdDev, "canvas luminance is flat").toBeGreaterThan(4);

  const moved = changedPixels(first, second);
  expect(moved / stats.pixels, "the frame did not advance").toBeGreaterThan(0.005);

  // Fit mode: the canvas is letterboxed to 16:9 at a 1280x720 viewport, and not stretched.
  const box = await canvas.boundingBox();
  expect(box.width).toBeCloseTo(1280, 0);
  expect(box.height).toBeCloseTo(720, 0);

  expect(failedRequests, failedRequests.join("\n")).toEqual([]);
  expect(errors, errors.join("\n")).toEqual([]);
});

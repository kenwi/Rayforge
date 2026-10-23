const path = require("node:path");
const { defineConfig } = require("@playwright/test");

// SMOKE_URL runs the same checks against a deployed site (for example
// https://kenwi.github.io); SMOKE_ROOT/SMOKE_PORT serve a local publish instead.
const liveOrigin = process.env.SMOKE_URL;
const siteRoot = process.env.SMOKE_ROOT || path.resolve(__dirname, "../../artifacts/site/Rayforge");
const port = Number(process.env.SMOKE_PORT || 4173);
const baseURL = liveOrigin || `http://127.0.0.1:${port}`;

module.exports = defineConfig({
  testDir: __dirname,
  testMatch: "smoke.spec.js",
  fullyParallel: false,
  workers: 1,
  timeout: 120_000,
  expect: { timeout: 30_000 },
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : [["list"]],
  use: {
    baseURL,
    viewport: { width: 1280, height: 720 },
    deviceScaleFactor: 1,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    launchOptions: {
      executablePath: process.env.CHROMIUM_PATH || undefined,
      args: [
        // Headless Chromium has no GPU; render WebGL through SwiftShader.
        "--use-gl=angle",
        "--use-angle=swiftshader",
        "--enable-unsafe-swiftshader",
        "--disable-dev-shm-usage",
      ],
    },
  },
  webServer: liveOrigin
    ? undefined
    : {
        command: `node ${JSON.stringify(path.join(__dirname, "server.js"))} ${JSON.stringify(siteRoot)} ${port}`,
        url: `${baseURL}/Rayforge/`,
        reuseExistingServer: !process.env.CI,
        timeout: 30_000,
      },
});

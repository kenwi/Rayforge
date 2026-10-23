#!/usr/bin/env node
// Minimal static server for the smoke test. It deliberately serves the app under a sub-path
// (/Rayforge/) so the same <base href="./" /> that GitHub Pages uses is exercised.
//
//   node server.js <root> [port] [prefix]
//
// <root> contains the published wwwroot (index.html, _framework/, assets/).

const http = require("node:http");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(process.argv[2] || ".");
const port = Number(process.argv[3] || 4173);
const prefix = (process.argv[4] || "/Rayforge/").replace(/\/?$/, "/");

const mime = new Map([
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
  [".mjs", "text/javascript; charset=utf-8"],
  [".css", "text/css; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".wasm", "application/wasm"],
  [".webcil", "application/octet-stream"],
  [".dat", "application/octet-stream"],
  [".dll", "application/octet-stream"],
  [".pdb", "application/octet-stream"],
  [".png", "image/png"],
  [".jpg", "image/jpeg"],
  [".svg", "image/svg+xml"],
  [".ico", "image/x-icon"],
  [".ttf", "font/ttf"],
  [".woff", "font/woff"],
  [".woff2", "font/woff2"],
  [".ogg", "audio/ogg"],
  [".wav", "audio/wav"],
  [".mp3", "audio/mpeg"],
  [".txt", "text/plain; charset=utf-8"],
]);

function send(response, status, body, type = "text/plain; charset=utf-8") {
  response.writeHead(status, {
    "Content-Type": type,
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
  });
  response.end(body);
}

const server = http.createServer((request, response) => {
  const url = new URL(request.url, `http://${request.headers.host || "localhost"}`);

  if (!url.pathname.startsWith(prefix)) {
    if (url.pathname === "/") {
      response.writeHead(302, { Location: prefix });
      response.end();
      return;
    }
    send(response, 404, "Not found");
    return;
  }

  let relative = decodeURIComponent(url.pathname.slice(prefix.length));
  if (relative === "" || relative.endsWith("/")) {
    relative += "index.html";
  }

  const file = path.resolve(root, relative);
  if (file !== root && !file.startsWith(root + path.sep)) {
    send(response, 403, "Forbidden");
    return;
  }

  fs.stat(file, (statError, stats) => {
    if (statError || !stats.isFile()) {
      send(response, 404, "Not found");
      return;
    }

    response.writeHead(200, {
      "Content-Type": mime.get(path.extname(file).toLowerCase()) || "application/octet-stream",
      "Content-Length": stats.size,
      "Cache-Control": "no-store",
      "X-Content-Type-Options": "nosniff",
    });

    if (request.method === "HEAD") {
      response.end();
      return;
    }

    fs.createReadStream(file).pipe(response);
  });
});

server.listen(port, "127.0.0.1", () => {
  console.log(`serving ${root} at http://127.0.0.1:${port}${prefix}`);
});

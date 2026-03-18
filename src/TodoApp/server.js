const http = require("http");
const fs = require("fs");
const path = require("path");

const port = Number(process.env.PORT || 80);
const root = path.join(__dirname, "public");

const mimeTypes = {
  ".html": "text/html; charset=utf-8",
  ".js": "application/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".ico": "image/x-icon",
  ".svg": "image/svg+xml",
  ".png": "image/png"
};

function sendJson(response, statusCode, payload) {
  response.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(payload));
}

function sendFile(response, filePath) {
  const extension = path.extname(filePath).toLowerCase();
  const contentType = mimeTypes[extension] || "application/octet-stream";
  response.writeHead(200, { "Content-Type": contentType });
  fs.createReadStream(filePath).pipe(response);
}

const server = http.createServer((request, response) => {
  const requestUrl = new URL(request.url || "/", `http://${request.headers.host || "localhost"}`);

  if (requestUrl.pathname === "/health") {
    sendJson(response, 200, { status: "healthy" });
    return;
  }

  const safePath = path.normalize(decodeURIComponent(requestUrl.pathname)).replace(/^\.+/, "");
  const candidate = path.join(root, safePath);
  const fallback = path.join(root, "index.html");

  fs.stat(candidate, (error, stats) => {
    if (!error && stats.isFile()) {
      sendFile(response, candidate);
      return;
    }

    if (!error && stats.isDirectory()) {
      const indexInDirectory = path.join(candidate, "index.html");
      fs.stat(indexInDirectory, (directoryError, directoryStats) => {
        if (!directoryError && directoryStats.isFile()) {
          sendFile(response, indexInDirectory);
        } else {
          sendFile(response, fallback);
        }
      });
      return;
    }

    sendFile(response, fallback);
  });
});

server.listen(port, () => {
  console.log(`Todo App server listening on port ${port}`);
});


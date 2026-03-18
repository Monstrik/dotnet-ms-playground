FROM mcr.microsoft.com/devcontainers/javascript-node:1-20-bookworm

WORKDIR /app

COPY src/Monitor/wwwroot/ /app/public/
COPY src/PlainJsClient/server.js /app/server.js

EXPOSE 80

CMD ["node", "/app/server.js"]


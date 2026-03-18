FROM mcr.microsoft.com/devcontainers/javascript-node:1-20-bookworm

WORKDIR /app

COPY src/TodoApp/public/ /app/public/
COPY src/TodoApp/server.js /app/server.js

EXPOSE 80

CMD ["node", "/app/server.js"]


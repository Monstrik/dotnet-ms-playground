# Solution2 - Echo Microservice

Minimal ASP.NET Core echo API with a plain JavaScript client.

## Structure

- `src/EchoService` - Web API service
- `src/EchoClient` - static plain JavaScript client host
- `tests/EchoService.Tests` - integration tests for API endpoints
- `Solution2.sln` - solution entry point

## Endpoints

- `GET /` - service metadata and status
- `GET /health` - liveness probe (`healthy`)
- `GET /echo/{message}` - echoes route message
- `POST /echo` - echoes JSON message payload

Example POST payload:

```json
{ "message": "hello" }
```

## Local run

```bash
dotnet restore
dotnet run --project src/EchoService/EchoService.csproj --urls http://localhost:5037
```

Service URL default from launch profile is `http://localhost:5037` (or use the URL printed by ASP.NET Core).

Run the client host locally (separate terminal):

```bash
dotnet run --project src/EchoClient/EchoClient.csproj --urls http://localhost:5050
```

Open the JavaScript client in your browser:

```bash
open "http://localhost:5050/?api=http://localhost:5037"
```

If your local run uses different ports, pass the API URL with `?api=...`.

## Test

```bash
dotnet test
```

## Docker run

Build from repository root so the Docker context includes `src/`:

```bash
docker build -f src/EchoService/Dockerfile -t echo-service:local .
docker run --rm -p 8080:8080 echo-service:local
```

Then call:

```bash
curl http://localhost:8080/health
curl http://localhost:8080/echo/hello
curl -X POST http://localhost:8080/echo -H 'Content-Type: application/json' -d '{"message":"hello"}'
```

## Docker Compose (graceful start/stop)

Compose runs **two containers**:

- `echo-service` (ASP.NET API, host `8082` -> container `8080`)
- `echo-client` (ASP.NET static host, host `8080` -> container `8080`)

Use compose to build and start in detached mode:

```bash
docker compose up -d --build
```

Check logs and verify API and UI:

```bash
docker compose logs -f echo-service
docker compose logs -f echo-client
curl http://localhost:8082/health
curl http://localhost:8082/echo/hello
open http://localhost:8080
```

In this setup, the browser app calls `http://localhost:8082` directly from `http://localhost:8080`.

Stop gracefully (30s grace period from `docker-compose.yml`):

```bash
docker compose stop -t 30 echo-client echo-service
docker compose down
```


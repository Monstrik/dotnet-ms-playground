# Solution2 - Echo Microservice

Minimal ASP.NET Core echo microservice with Docker support.

## Structure

- `src/EchoService` - Web API service
- `tests/EchoService.Tests` - integration tests for API endpoints
- `Solution2.sln` - solution entry point

## Endpoints

- `GET /` - service metadata and status
- `GET /client` - browser client (plain JavaScript)
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
dotnet run --project src/EchoService/EchoService.csproj
```

Service URL default from launch profile is `http://localhost:5037` (or use the URL printed by ASP.NET Core).

To force a deterministic local port:

```bash
dotnet run --project src/EchoService/EchoService.csproj --urls http://localhost:5037
```

Open the JavaScript client in your browser:

```bash
open http://localhost:5037/client
```

If your local run uses a different port, replace `5037` with the URL shown in terminal.

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

Use compose to build and start in detached mode:

```bash
docker compose up -d --build
```

Check logs and verify the service:

```bash
docker compose logs -f echo-service
curl http://localhost:8080/health
open http://localhost:8080/client
```

Stop gracefully (30s grace period from `docker-compose.yml`):

```bash
docker compose stop -t 30 echo-service
docker compose down
```


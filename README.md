# Solution2 - Microservices Demo

Minimal ASP.NET Core microservices demo with an echo API, a weather API, and a plain JavaScript monitor.

## Structure

- `src/EchoService` - Web API service
- `src/WeatherService` - Web API service for sample weather data
- `src/Monitor` - static plain JavaScript monitor host
- `src/PlainJsClient` - container-only static host files for a non-.NET plain JS client option
- `tests/EchoService.Tests` - integration tests for echo API endpoints
- `tests/WeatherService.Tests` - integration tests for weather API endpoints
- `Solution2.sln` - solution entry point

## Endpoints

- `EchoService`
  - `GET /` - service metadata and status
  - `GET /health` - liveness probe (`healthy`)
  - `GET /echo/{message}` - echoes route message
  - `POST /echo` - echoes JSON message payload
- `WeatherService`
  - `GET /` - service metadata and status
  - `GET /health` - liveness probe (`healthy`)
  - `GET /weather` - returns a default-city forecast (`Seattle`)
  - `GET /weather/{city}` - returns a deterministic sample forecast for the requested city

Example POST payload:

```json
{ "message": "hello" }
```

## Local run

```bash
dotnet restore
dotnet run --project src/EchoService/EchoService.csproj --urls http://localhost:5037
```

Run the weather service locally (separate terminal):

```bash
dotnet run --project src/WeatherService/WeatherService.csproj --urls http://localhost:5047
```

Run the monitor host locally (separate terminal):

```bash
dotnet run --project src/Monitor/Monitor.csproj --urls http://localhost:5050
```

Open the monitor in your browser:

```bash
open "http://localhost:5050/?api=http://localhost:5037&weatherApi=http://localhost:5047"
```

The monitor homepage includes a shared health dashboard for monitor, echo, and weather services.

If your local run uses different ports, pass the service URLs with `?api=...&weatherApi=...`.

## Test

```bash
dotnet test
```

## Docker run

Build the echo service from repository root so the Docker context includes `src/`:

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

Build and run the weather service separately:

```bash
docker build -f src/WeatherService/Dockerfile -t weather-service:local .
docker run --rm -p 8084:8080 weather-service:local
```

Then call:

```bash
curl http://localhost:8084/health
curl http://localhost:8084/weather
curl http://localhost:8084/weather/Tokyo
```

## Docker Compose (graceful start/stop)

Compose runs **four containers**:

- `echo-service` (ASP.NET API, host `8082` -> container `8080`)
- `weather-service` (ASP.NET API, host `8084` -> container `8080`)
- `monitor` (ASP.NET static host, host `8080` -> container `8080`)
- `plain-js-client` (Node static host, host `8086` -> container `80`)

Use compose to build and start in detached mode:

```bash
docker compose up -d --build
```

Or use the helper script from repo root:

```bash
./scripts/dev-up.sh
```

Check logs and verify API and UI:

```bash
docker compose logs -f echo-service
docker compose logs -f weather-service
docker compose logs -f monitor
docker compose logs -f plain-js-client
curl http://localhost:8082/health
curl http://localhost:8082/echo/hello
curl http://localhost:8084/health
curl http://localhost:8084/weather/London
open http://localhost:8080
open http://localhost:8086
```

In this setup, both browser UIs call `http://localhost:8082` and `http://localhost:8084` directly.

Stop gracefully (30s grace period from `docker-compose.yml`):

```bash
docker compose stop -t 30 monitor plain-js-client echo-service weather-service
docker compose down
```

Or use the helper script:

```bash
./scripts/dev-down.sh
```


# AGENTS Guide for `Solution2`

## Repository Reality Check
- This repo now contains runnable .NET 8 APIs, browser monitor hosts, and tests.
- The solution entry point is `Solution2.sln` with seven projects:
  - `src/EchoService/EchoService.csproj`
  - `src/WeatherService/WeatherService.csproj`
  - `src/TodoService/TodoService.csproj`
  - `src/Monitor/Monitor.csproj`
  - `tests/EchoService.Tests/EchoService.Tests.csproj`
  - `tests/WeatherService.Tests/WeatherService.Tests.csproj`
  - `tests/TodoService.Tests/TodoService.Tests.csproj`
- `.idea/.idea.Solution2/.idea/*` remains IDE metadata only.

## Big-Picture Architecture
- `src/EchoService/Program.cs` is a minimal API (top-level statements) with all HTTP routes defined inline.
- `src/WeatherService/Program.cs` is another minimal API that returns deterministic sample weather data.
- `src/TodoService/Program.cs` is a minimal API that provides todo CRUD and uses Postgres when `TODO_DB_CONNECTION` is configured.
- `src/Monitor/Program.cs` is a minimal static-file host serving the plain JavaScript monitor UI from `wwwroot`.
- Docker Compose runs seven containers; browser traffic can hit `monitor` (port `8080`), `plain-js-client` (port `8086`), or `todo-app` (port `8087`), and all call `echo-service` (`8082`), `weather-service` (`8084`), and `todo-service` (`8088`).
- Request flow remains direct: route -> minimal handler -> JSON response; no mediator/service layers yet.
- API contract examples:
  - `GET /health` returns `{ "status": "healthy" }`
  - `GET /echo/{message}` returns `{ "message": "..." }`
  - `GET /weather/{city}` returns a JSON forecast for the requested city.
  - `POST /todos` with `{ "title": "..." }` returns a created todo item.

## Developer Workflows
- Restore/build/test from repo root:
  - `dotnet restore`
  - `dotnet build`
  - `dotnet test`
- Run locally (four terminals):
  - `dotnet run --project src/EchoService/EchoService.csproj --urls http://localhost:5037`
  - `dotnet run --project src/WeatherService/WeatherService.csproj --urls http://localhost:5047`
  - `dotnet run --project src/TodoService/TodoService.csproj --urls http://localhost:5067`
  - `dotnet run --project src/Monitor/Monitor.csproj --urls http://localhost:5050`
- Docker workflow (from repo root):
  - `docker compose up -d --build`
  - `docker compose down`
  - `./scripts/dev-up.sh`
  - `./scripts/dev-down.sh`

## Project-Specific Conventions
- Keep solution registration explicit in `Solution2.sln` when adding/removing projects.
- Favor minimal API style for small endpoints unless complexity justifies extracting layers.
- Keep root lean; place runtime code under `src/` and tests under `tests/`.
- Keep browser UI logic in `src/Monitor/wwwroot` as plain JS; avoid adding frontend build tools unless needed.
- `src/PlainJsClient` contains container-only static host files that serve the same monitor assets for a non-.NET plain JS host option.
- `src/TodoApp` contains a dedicated Node static host with its own pure JavaScript todo client UI serving from `public/`.
- Integration tests use `WebApplicationFactory<Program>`; keep each service `Program` test-visible via `public partial class Program;`.

## Integration & Dependency Notes
- Runtime dependencies are framework-only for hosts plus `Npgsql` in `src/TodoService/TodoService.csproj`.
- Test-only dependencies live in `tests/EchoService.Tests/EchoService.Tests.csproj`, `tests/WeatherService.Tests/WeatherService.Tests.csproj`, and `tests/TodoService.Tests/TodoService.Tests.csproj` (xUnit + ASP.NET Core test host).
- Browser-to-service communication uses CORS, so keep allowed origins aligned with documented local and Compose ports.
- Compose includes `postgres`; `todo-service` uses `TODO_DB_CONNECTION` to connect internally.
- No CI/CD config, container orchestration manifests, or external package pinning files are present yet.

## Guidance for Future Agent Changes
- If adding business logic, move non-trivial behavior from inline handlers to dedicated classes under `src/EchoService` or `src/WeatherService`.
- If adding business logic, move non-trivial behavior from inline handlers to dedicated classes under `src/EchoService`, `src/WeatherService`, or `src/TodoService`.
- If adding integrations (DB, broker, external APIs), document setup and local-dev expectations in `README.md` and this file.
- Keep examples and commands evidence-based and validated against current repository files.


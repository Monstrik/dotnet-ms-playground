# AGENTS Guide for `Solution2`

## Repository Reality Check
- This repo now contains runnable .NET 8 APIs, browser monitor hosts, and tests.
- The solution entry point is `Solution2.sln` with ten projects:
  - `src/EchoService/EchoService.csproj`
  - `src/WeatherService/WeatherService.csproj`
  - `src/TodoService/TodoService.csproj`
  - `src/Monitor/Monitor.csproj`
  - `src/OrderProducerService/OrderProducerService.csproj`
  - `src/KitchenService/KitchenService.csproj`
  - `src/DeliveryService/DeliveryService.csproj`
  - `tests/EchoService.Tests/EchoService.Tests.csproj`
  - `tests/WeatherService.Tests/WeatherService.Tests.csproj`
  - `tests/TodoService.Tests/TodoService.Tests.csproj`
- `.idea/.idea.Solution2/.idea/*` remains IDE metadata only.

## Big-Picture Architecture
- `src/EchoService/Program.cs` is a minimal API (top-level statements) with all HTTP routes defined inline.
- `src/WeatherService/Program.cs` is another minimal API that returns deterministic sample weather data.
- `src/TodoService/Program.cs` is a minimal API that provides todo CRUD and uses Postgres when `TODO_DB_CONNECTION` is configured.
- `src/Monitor/Program.cs` is a minimal static-file host serving the plain JavaScript monitor UI from `wwwroot`.
- `src/OrderProducerService/Program.cs` is a minimal API plus background worker that generates random food orders and publishes to `orders.new`.
- `src/KitchenService/Program.cs` is a minimal API plus background worker that consumes `orders.new`, simulates prep, and publishes to `orders.readyForDelivery`.
- `src/DeliveryService/Program.cs` is a minimal API plus background worker that consumes `orders.readyForDelivery`, simulates delivery, and publishes to `orders.delivered`.
- Docker Compose runs eleven containers; browser traffic can hit `monitor` (port `8080`), `plain-js-client` (port `8086`), or `todo-app` (port `8087`). Workflow control uses `order-producer-service` (`8091`), `kitchen-service` (`8092`), and `delivery-service` (`8093`) over RabbitMQ (`5672`).
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
- Run food-order workflow locally (three additional terminals, RabbitMQ required):
  - `dotnet run --project src/OrderProducerService/OrderProducerService.csproj --urls http://localhost:5077`
  - `dotnet run --project src/KitchenService/KitchenService.csproj --urls http://localhost:5087`
  - `dotnet run --project src/DeliveryService/DeliveryService.csproj --urls http://localhost:5097`
- Docker workflow (from repo root):
  - `docker compose up -d --build`
  - `docker compose down`
  - `./scripts/dev-up.sh`
  - `./scripts/dev-down.sh`
- Minikube workflow (from repo root):
  - `./scripts/minikube-up.sh` — starts minikube, builds all images inside minikube's Docker daemon, applies all manifests, waits for readiness
  - In a **separate terminal**, run `./scripts/minikube-access.sh` — establishes `kubectl port-forward` tunnels so all services are accessible via `localhost:808X`
  - `./scripts/minikube-down.sh` — stops all port-forwards, deletes the `foodorder` namespace, and stops minikube
  - Access the dashboard at `http://localhost:8080` (automatically resolves to workflow APIs on `localhost:8091`, `localhost:8092`, `localhost:8093`)
  - All manifests live in `k8s/` — one file per service plus `namespace.yaml`
  - Images use `imagePullPolicy: Never`; they are built inside minikube's daemon during `minikube-up.sh`
  - **Note:** On macOS with Docker driver, NodePort services are not directly accessible from the host; the `minikube-access.sh` script uses `kubectl port-forward` to expose them on localhost

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
- Compose includes `postgres` and `rabbitmq`; `todo-service` uses `TODO_DB_CONNECTION` to connect internally, and future broker integrations can use host `rabbitmq` on port `5672`.
- No CI/CD config, container orchestration manifests, or external package pinning files are present yet.

## Guidance for Future Agent Changes
- If adding business logic, move non-trivial behavior from inline handlers to dedicated classes under `src/EchoService` or `src/WeatherService`.
- If adding business logic, move non-trivial behavior from inline handlers to dedicated classes under `src/EchoService`, `src/WeatherService`, or `src/TodoService`.
- If adding integrations (DB, broker, external APIs), document setup and local-dev expectations in `README.md` and this file.
- Keep examples and commands evidence-based and validated against current repository files.


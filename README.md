# Solution2 - Microservices Demo

Minimal ASP.NET Core microservices demo with echo, weather, and todo APIs plus browser monitor clients.

This repository also includes a RabbitMQ-powered food-order workflow with runtime orchestration from the monitor dashboard.

## Structure

- `src/EchoService` - Web API service
- `src/WeatherService` - Web API service for sample weather data
- `src/TodoService` - Web API service for simple todo CRUD
- `src/Monitor` - static plain JavaScript monitor host
- `src/OrderProducerService` - generates random food orders and publishes to `orders.new`
- `src/KitchenService` - consumes `orders.new`, simulates preparation, publishes to `orders.readyForDelivery`
- `src/DeliveryService` - consumes `orders.readyForDelivery`, simulates delivery, publishes to `orders.delivered`
- `src/PlainJsClient` - container-only static host files for a non-.NET plain JS client option
- `src/TodoApp` - dedicated todo application client (Node static host)
- `tests/EchoService.Tests` - integration tests for echo API endpoints
- `tests/WeatherService.Tests` - integration tests for weather API endpoints
- `tests/TodoService.Tests` - integration tests for todo API endpoints
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
- `TodoService`
  - `GET /` - service metadata and status
  - `GET /health` - liveness probe (`healthy`) and storage mode
  - `GET /todos` - list todo items
  - `POST /todos` - create todo (`{ "title": "..." }`)
  - `PATCH /todos/{id}` - mark completion (`{ "isCompleted": true|false }`)
  - `DELETE /todos/{id}` - remove todo

- `OrderProducerService`
  - `GET /health` - liveness + running state
  - `GET /config` - current producer config
  - `PUT /config` - update `{ "ordersPerMinute": number }`
  - `POST /control/start` - start producing
  - `POST /control/stop` - stop producing
  - `GET /stats` - produced counters and recent orders
  - `GET /queues` - RabbitMQ queue metrics for `orders.new`, `orders.readyForDelivery`, `orders.delivered`
  - `POST /admin/reset` - purge workflow queues and reset producer state

- `KitchenService`
  - `GET /health` - liveness + running state
  - `GET /config` - current preparation range
  - `PUT /config` - update `{ "minPreparationSeconds": number, "maxPreparationSeconds": number }`
  - `POST /control/start` - start consuming
  - `POST /control/stop` - stop consuming
  - `GET /stats` - processing count, prepared count, in-progress/ready orders
  - `POST /admin/reset-state` - clear in-memory workflow state

- `DeliveryService`
  - `GET /health` - liveness + running state
  - `GET /config` - current delivery range
  - `PUT /config` - update `{ "minDeliverySeconds": number, "maxDeliverySeconds": number }`
  - `POST /control/start` - start consuming
  - `POST /control/stop` - stop consuming
  - `GET /stats` - processing count, delivered count, in-progress/delivered orders
  - `POST /admin/reset-state` - clear in-memory workflow state

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

Run the todo service locally (separate terminal):

```bash
dotnet run --project src/TodoService/TodoService.csproj --urls http://localhost:5067
```

Run the monitor host locally (separate terminal):

```bash
dotnet run --project src/Monitor/Monitor.csproj --urls http://localhost:5050
```

Run the RabbitMQ workflow services locally (separate terminals):

```bash
dotnet run --project src/OrderProducerService/OrderProducerService.csproj --urls http://localhost:5077
dotnet run --project src/KitchenService/KitchenService.csproj --urls http://localhost:5087
dotnet run --project src/DeliveryService/DeliveryService.csproj --urls http://localhost:5097
```

Open the monitor in your browser:

```bash
open "http://localhost:5050/?api=http://localhost:5037&weatherApi=http://localhost:5047&todoApi=http://localhost:5067"
```

Open the workflow dashboard with explicit local service URLs:

```bash
open "http://localhost:5050/?orderApi=http://localhost:5077&kitchenApi=http://localhost:5087&deliveryApi=http://localhost:5097"
```

The monitor homepage now provides the workflow dashboard for producer, kitchen, and delivery orchestration, queue metrics, and live order status.

If your local run uses different ports, pass the service URLs with `?orderApi=...&kitchenApi=...&deliveryApi=...`.

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

Build and run the todo service separately (in-memory mode):

```bash
docker build -f src/TodoService/Dockerfile -t todo-service:local .
docker run --rm -p 8088:8080 todo-service:local
```

Then call:

```bash
curl http://localhost:8088/health
curl http://localhost:8088/todos
curl -X POST http://localhost:8088/todos -H 'Content-Type: application/json' -d '{"title":"first todo"}'
```

## Docker Compose (graceful start/stop)

Compose runs **eleven containers**:

- `postgres` (PostgreSQL, host `5433` -> container `5432`)
- `rabbitmq` (RabbitMQ, host `5672` -> container `5672`)
- `echo-service` (ASP.NET API, host `8082` -> container `8080`)
- `weather-service` (ASP.NET API, host `8084` -> container `8080`)
- `todo-service` (ASP.NET API, host `8088` -> container `8080`, backed by `postgres`)
- `order-producer-service` (ASP.NET API, host `8091` -> container `8080`, produces workflow orders)
- `kitchen-service` (ASP.NET API, host `8092` -> container `8080`, prepares orders)
- `delivery-service` (ASP.NET API, host `8093` -> container `8080`, delivers orders)
- `monitor` (ASP.NET static host, host `8080` -> container `8080`)
- `plain-js-client` (Node static host, host `8086` -> container `80`)
- `todo-app` (Node static host for todo app, host `8087` -> container `80`)

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
docker compose logs -f todo-service
docker compose logs -f order-producer-service
docker compose logs -f kitchen-service
docker compose logs -f delivery-service
docker compose logs -f postgres
docker compose logs -f rabbitmq
docker compose logs -f monitor
docker compose logs -f plain-js-client
docker compose logs -f todo-app
docker compose exec -T rabbitmq rabbitmq-diagnostics -q ping
curl http://localhost:8082/health
curl http://localhost:8082/echo/hello
curl http://localhost:8084/health
curl http://localhost:8084/weather/London
curl http://localhost:8088/health
curl http://localhost:8088/todos
curl http://localhost:8091/health
curl http://localhost:8092/health
curl http://localhost:8093/health
open http://localhost:8080
open http://localhost:8086
open http://localhost:8087
```

In this setup, the workflow dashboard uses `http://localhost:8091`, `http://localhost:8092`, and `http://localhost:8093` for orchestration and queue monitoring.

RabbitMQ is also available to other containers at hostname `rabbitmq` and from the host at `localhost:5672` using the local development credentials `app` / `app`.

Stop gracefully (30s grace period from `docker-compose.yml`):

```bash
docker compose stop -t 30 monitor plain-js-client todo-app echo-service weather-service todo-service postgres rabbitmq
docker compose down
```

To include the workflow services in graceful stop explicitly:

```bash
docker compose stop -t 30 monitor plain-js-client todo-app echo-service weather-service todo-service order-producer-service kitchen-service delivery-service postgres rabbitmq
docker compose down
```

Or use the helper script:

```bash
./scripts/dev-down.sh
```


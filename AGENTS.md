# AGENTS Guide for `Solution2`

## Repository Reality Check
- This repo now contains a runnable .NET 8 microservice and tests.
- The solution entry point is `Solution2.sln` with two projects:
  - `src/EchoService/EchoService.csproj`
  - `tests/EchoService.Tests/EchoService.Tests.csproj`
- `.idea/.idea.Solution2/.idea/*` remains IDE metadata only.

## Big-Picture Architecture
- `src/EchoService/Program.cs` is a minimal API (top-level statements) with all HTTP routes defined inline.
- Current service boundary is a single-process HTTP API; no database, queue, or external service clients.
- Request flow is direct: route -> minimal handler -> JSON response; no mediator/service layers yet.
- API contract examples:
  - `GET /health` returns `{ "status": "healthy" }`
  - `GET /echo/{message}` returns `{ "message": "..." }`
  - `POST /echo` accepts `{ "message": "..." }` and echoes it back.

## Developer Workflows
- Restore/build/test from repo root:
  - `dotnet restore`
  - `dotnet build`
  - `dotnet test`
- Run service locally:
  - `dotnet run --project src/EchoService/EchoService.csproj`
- Docker workflow (from repo root):
  - `docker build -f src/EchoService/Dockerfile -t echo-service:local .`
  - `docker run --rm -p 8080:8080 echo-service:local`

## Project-Specific Conventions
- Keep solution registration explicit in `Solution2.sln` when adding/removing projects.
- Favor minimal API style for small endpoints unless complexity justifies extracting layers.
- Keep root lean; place runtime code under `src/` and tests under `tests/`.
- Integration tests use `WebApplicationFactory<Program>`; keep `Program` test-visible via `public partial class Program;`.

## Integration & Dependency Notes
- Runtime dependencies are framework-only (`Microsoft.NET.Sdk.Web` in `src/EchoService/EchoService.csproj`).
- Test-only dependencies live in `tests/EchoService.Tests/EchoService.Tests.csproj` (xUnit + ASP.NET Core test host).
- No CI/CD config, container orchestration manifests, or external package pinning files are present yet.

## Guidance for Future Agent Changes
- If adding business logic, move non-trivial behavior from inline handlers to dedicated classes under `src/EchoService`.
- If adding integrations (DB, broker, external APIs), document setup and local-dev expectations in `README.md` and this file.
- Keep examples and commands evidence-based and validated against current repository files.


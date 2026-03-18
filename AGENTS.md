# AGENTS Guide for `Solution2`

## Repository Reality Check
- This repo now contains a runnable .NET 8 API, a browser client host, and tests.
- The solution entry point is `Solution2.sln` with three projects:
  - `src/EchoService/EchoService.csproj`
  - `src/EchoClient/EchoClient.csproj`
  - `tests/EchoService.Tests/EchoService.Tests.csproj`
- `.idea/.idea.Solution2/.idea/*` remains IDE metadata only.

## Big-Picture Architecture
- `src/EchoService/Program.cs` is a minimal API (top-level statements) with all HTTP routes defined inline.
- `src/EchoClient/Program.cs` is a minimal static-file host serving the plain JavaScript UI from `wwwroot`.
- Docker Compose runs client and API as separate containers; browser traffic hits `echo-client`, which calls API on host port `8082`.
- Request flow remains direct: route -> minimal handler -> JSON response; no mediator/service layers yet.
- API contract examples:
  - `GET /health` returns `{ "status": "healthy" }`
  - `GET /echo/{message}` returns `{ "message": "..." }`
  - `POST /echo` accepts `{ "message": "..." }` and echoes it back.

## Developer Workflows
- Restore/build/test from repo root:
  - `dotnet restore`
  - `dotnet build`
  - `dotnet test`
- Run locally (two terminals):
  - `dotnet run --project src/EchoService/EchoService.csproj --urls http://localhost:5037`
  - `dotnet run --project src/EchoClient/EchoClient.csproj --urls http://localhost:5050`
- Docker workflow (from repo root):
  - `docker compose up -d --build`
  - `docker compose down`

## Project-Specific Conventions
- Keep solution registration explicit in `Solution2.sln` when adding/removing projects.
- Favor minimal API style for small endpoints unless complexity justifies extracting layers.
- Keep root lean; place runtime code under `src/` and tests under `tests/`.
- Keep browser UI logic in `src/EchoClient/wwwroot` as plain JS; avoid adding frontend build tools unless needed.
- Integration tests use `WebApplicationFactory<Program>`; keep `Program` test-visible via `public partial class Program;`.

## Integration & Dependency Notes
- Runtime dependencies are framework-only (`Microsoft.NET.Sdk.Web` in `src/EchoService/EchoService.csproj` and `src/EchoClient/EchoClient.csproj`).
- Test-only dependencies live in `tests/EchoService.Tests/EchoService.Tests.csproj` (xUnit + ASP.NET Core test host).
- No CI/CD config, container orchestration manifests, or external package pinning files are present yet.

## Guidance for Future Agent Changes
- If adding business logic, move non-trivial behavior from inline handlers to dedicated classes under `src/EchoService`.
- If adding integrations (DB, broker, external APIs), document setup and local-dev expectations in `README.md` and this file.
- Keep examples and commands evidence-based and validated against current repository files.


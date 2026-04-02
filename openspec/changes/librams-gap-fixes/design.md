## Context

LibraMS was built as an academic assignment demonstrating a production-quality full-stack system. The initial implementation is functionally complete per its task list, but several defects and omissions were found during review:

- Two startup-blocking issues: missing `UserRepository` (registered in DI) and missing `tsconfig.json` (required by build script)
- A silent data bug: `BookStatus.CheckedOut` serializes to `"checkedout"` instead of the DB's `"checked_out"`, breaking status filtering and checkout flows
- No `.gitignore`, causing build artifacts (`obj/`) to be tracked in git
- No tests at any layer (backend or frontend)
- No protection on AI endpoints against quota exhaustion
- Missing input validation on book updates
- No health check for Azure App Service probing

The system uses .NET 8 Minimal API (Carter) on the backend and React 18 + TypeScript + Vite on the frontend, with Supabase for auth/DB and Groq AI for three AI features.

## Goals / Non-Goals

**Goals:**
- Eliminate both startup blockers so the app runs locally and on Azure
- Fix the enum serialization bug so book status works correctly end-to-end
- Add a `.gitignore` that covers all standard artifacts for this stack
- Add a backend test project (`LibraMS.Api.Tests`) with unit tests for repositories and integration tests for key endpoints
- Add frontend test setup (Vitest + React Testing Library) with tests for hooks and critical components
- Add ASP.NET Core rate limiting middleware scoped to `/api/ai/*`
- Add `UpdateBookValidator` mirroring `CreateBookValidator` rules
- Add `GET /health` endpoint returning `200 OK`
- Update README with real URLs and pg_cron setup instructions
- Add test steps to both GitHub Actions workflows

**Non-Goals:**
- End-to-end browser tests (Playwright/Cypress) — out of scope for this fix pass
- Load testing or performance benchmarking
- Changing any existing feature behavior beyond the bug fix
- Adding a database migration framework

## Decisions

### D1: Fix enum serialization via explicit string mapping, not attribute decoration

**Decision**: Change `BookStatus` → SQL string conversion to explicitly return `"available"` / `"checked_out"` using a switch expression, rather than relying on `.ToString().ToLower().Replace("_","")`.

**Rationale**: The current approach is fragile — enum value naming directly controls DB column values. A switch expression makes the mapping explicit and verifiable. Using `[JsonConverter]` or a Dapper `TypeHandler` would also work, but the switch is the simplest targeted fix with zero new dependencies.

**Alternatives considered**:
- Rename the enum value to `CheckedOut` and fix the replace logic — still fragile, same root cause
- Add a Dapper `SqlMapper.TypeHandler<BookStatus>` — clean but more infrastructure for a one-enum problem

### D2: Backend tests use xUnit + real Npgsql against a test DB (not mocks)

**Decision**: Use xUnit with a `TestDbFixture` that connects to a real PostgreSQL instance (local or Supabase test project) for repository tests. Endpoint integration tests use `WebApplicationFactory<Program>`.

**Rationale**: The initial implementation was flagged for avoiding mocks after a prior incident where mock/prod divergence masked a broken migration (see project memory). Raw SQL via Dapper is also harder to mock meaningfully — a real DB connection provides genuine confidence.

**Alternatives considered**:
- Moq-based unit tests with mocked `IDbConnection` — fast but brittle for SQL-heavy code
- TestContainers (spin up Docker Postgres) — ideal but adds Docker dependency; noted as a future improvement

### D3: Frontend tests use Vitest (not Jest)

**Decision**: Use Vitest as the test runner since the project already uses Vite. Configure via `vite.config.ts` `test` block.

**Rationale**: Vitest shares the Vite config and transformation pipeline — zero additional transpile setup. Jest requires Babel or ts-jest configuration that diverges from the Vite build.

### D4: Rate limiting via ASP.NET Core built-in `RateLimiter` (no external packages)

**Decision**: Use `Microsoft.AspNetCore.RateLimiting` (built into .NET 8) with a fixed-window policy applied to the `/api/ai` route group.

**Rationale**: .NET 8 includes rate limiting middleware natively. Adding an external library (e.g., `AspNetCoreRateLimit`) for what is a simple per-IP fixed-window policy on three endpoints is unnecessary complexity.

**Policy choice**: Fixed window, 10 requests/minute per IP, returning `429 Too Many Requests`. This is conservative enough to protect the Groq free tier while not impacting normal use.

### D5: `UserRepository` implements only the methods already called

**Decision**: Implement `IUserRepository` with only `GetByIdAsync` and `EnsureExistsAsync` (upsert for new OAuth sign-ins), since these are the only methods referenced anywhere in the codebase.

**Rationale**: Don't add methods for hypothetical future needs. The `RoleEnrichmentMiddleware` only needs a role lookup, which already queries the DB directly. The interface should be minimal.

## Risks / Trade-offs

- **Real DB test dependency** → Tests require a configured PostgreSQL connection; CI will need a `TEST_DB_CONNECTION_STRING` secret. Mitigation: document clearly in README; tests are skipped gracefully if the env var is absent.
- **Rate limiting is per-instance** → On Azure F1 (single instance), the fixed-window limiter works correctly. If scaled out, limits would be per-instance not global. Mitigation: acceptable for free-tier demo; document the limitation.
- **Enum fix is a breaking change for any clients relying on the buggy behavior** → No known external clients; the frontend uses the API exclusively and will benefit from the fix.

## Migration Plan

1. Apply backend fixes first (enum, UserRepository, health check, validator, rate limiting) — `dotnet run` must succeed locally before proceeding
2. Add `.gitignore` and verify `obj/` is no longer tracked
3. Add `tsconfig.json` and verify `npm run build` succeeds
4. Add test projects and confirm they pass locally
5. Update GitHub Actions workflows to include test steps
6. Update README with real URLs and pg_cron docs
7. Deploy via normal `git push origin main` — both workflows fire

**Rollback**: All changes are additive or bug fixes. The enum fix is the only change that alters runtime behavior; rollback by reverting the switch expression in `BookRepository.cs`.

## Open Questions

- What PostgreSQL connection string should be used in CI for backend integration tests? (Needs a `TEST_DB_CONNECTION_STRING` secret added to GitHub repo settings — owner action required)
- Are the live Azure URLs (`librams.azurestaticapps.net`, `librams-api.azurewebsites.net`) the final deployed URLs to put in the README, or are they still placeholder names?

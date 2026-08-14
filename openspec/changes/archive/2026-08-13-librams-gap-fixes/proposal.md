## Why

LibraMS has multiple gaps identified after the initial implementation that range from startup-blocking bugs to missing tests and unsafe defaults. These must be addressed before the project can be considered production-ready or reliably deployable.

## What Changes

- **Fix startup blocker**: Add missing `IUserRepository` and `UserRepository` — registered in DI but never implemented, causing a runtime crash on startup
- **Fix build blocker**: Add missing `tsconfig.json` — required by the frontend build script (`tsc && vite build`)
- **Fix serialization bug**: Correct `BookStatus` enum → SQL string mapping (`"checkedout"` → `"checked_out"`) in `BookRepository`
- **Add `.gitignore`**: Prevent committing `obj/`, `bin/`, `node_modules/`, `.env.local`, and other artifacts
- **Add backend tests**: Unit tests for `BookRepository`, `LoanRepository`, and integration tests for API endpoints
- **Add frontend tests**: Component and hook tests using Vitest and React Testing Library
- **Add rate limiting**: Throttle `/api/ai/*` endpoints to protect the Groq free-tier quota
- **Add `UpdateBookValidator`**: FluentValidation rules for the PUT `/api/books/:id` endpoint
- **Add health check endpoint**: `GET /health` for Azure App Service probing and cold-start diagnostics
- **Update README**: Replace placeholder live demo URLs with real deployed URLs
- **Document pg_cron setup**: Add instructions for scheduling `mark_overdue_loans()` in Supabase
- **Archive initial change**: Run `/opsx:archive` on `librams-initial-implementation` after this change completes

## Capabilities

### New Capabilities

- `user-repository`: Read/write access to `library_users` table from the .NET API layer
- `backend-testing`: Automated test suite for repositories and API endpoints
- `frontend-testing`: Automated test suite for React components and data hooks
- `api-rate-limiting`: Per-IP request throttling on AI endpoints
- `health-check`: Liveness endpoint for infrastructure probing

### Modified Capabilities

- `book-management`: Fix status enum serialization; add update validation

## Impact

- **Backend**: New `UserRepository.cs`, new `Tests` project, rate limiting middleware, health endpoint, validator, enum fix
- **Frontend**: New `tsconfig.json`, new test files, Vitest config
- **Root**: New `.gitignore`
- **Docs**: README demo URLs, Supabase pg_cron setup section
- **CI**: No workflow changes needed — test step should be added to both workflows

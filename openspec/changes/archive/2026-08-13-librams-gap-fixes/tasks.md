## 0. Build Fixes (untracked gaps found during implementation)

- [x] 0.1 Fix `GroqAiService.cs`: change `$"""` to `$$"""` on all three prompt raw strings and update interpolation holes from `{expr}` to `{{expr}}` — the single-dollar form cannot contain literal JSON braces (CS9006)
- [x] 0.2 Create `backend/LibraMS.Api/Services/IAiService.cs` — the interface was registered in DI and used in endpoints but never defined, causing CS0246
- [x] 0.3 Fix `Program.cs` OpenAPI setup: `AddOpenApi()`/`MapOpenApi()` are .NET 9 APIs not available in .NET 8; replaced with `AddEndpointsApiExplorer()` + `AddSwaggerGen()` (Swashbuckle) and `UseSwagger()` with Scalar 2.x `MapScalarApiReference()` per the Scalar 2.x migration guide
- [x] 0.4 Add `Swashbuckle.AspNetCore 6.9.0` to `LibraMS.Api.csproj` — required by the .NET 8 + Scalar 2.x OpenAPI setup

## 1. Startup Blockers

- [x] 1.1 Create `backend/LibraMS.Api/Data/UserRepository.cs` with `IUserRepository` interface defining `GetByIdAsync(Guid id)` and `EnsureExistsAsync(Guid id, string email, string? displayName)`
- [x] 1.2 Implement `UserRepository` using Dapper — `GetByIdAsync` queries `public.library_users WHERE id = @id`; `EnsureExistsAsync` uses `INSERT ... ON CONFLICT (id) DO NOTHING`
- [x] 1.3 Verify `Program.cs` line 49 already registers `IUserRepository, UserRepository` — confirmed, no further DI changes needed
- [x] 1.4 Create `frontend/tsconfig.json` with `target: ES2020`, `lib: ["ES2020","DOM","DOM.Iterable"]`, `module: ESNext`, `moduleResolution: bundler`, `jsx: react-jsx`, `strict: true`, `paths` empty, referencing the `frontend/src` directory
- [x] 1.5 Run `npm run build` from `frontend/` and confirm it exits without errors

## 2. Bug Fix — BookStatus Serialization

- [x] 2.1 In `BookRepository.cs`, replace both `.ToString().ToLower().Replace("_","")` calls (lines ~41 and ~107) with an explicit switch expression: `BookStatus.Available => "available"`, `BookStatus.CheckedOut => "checked_out"`
- [x] 2.2 Verify that `SearchAsync` status filter and `SetStatusAsync` both use the corrected mapping
- [x] 2.3 Manually test checkout flow locally: borrow a book and confirm `books.status` is `"checked_out"` in the Supabase table editor

## 3. .gitignore

- [x] 3.1 Create `.gitignore` at the repository root covering: `**/bin/`, `**/obj/`, `node_modules/`, `dist/`, `.env.local`, `*.user`, `*.suo`, `.DS_Store`, `*.log`
- [x] 3.2 Run `git rm -r --cached backend/LibraMS.Api/obj/` to stop tracking the already-committed `obj/` artifacts

## 4. Health Check Endpoint

- [x] 4.1 In `Endpoints.cs` (or a new `HealthEndpoints.cs`), add a Carter module that maps `GET /health` returning `Results.Ok(new { status = "ok" })` with no authorization requirement
- [x] 4.2 Confirm `GET http://localhost:5000/health` returns `200 {"status":"ok"}` with no JWT

## 5. Rate Limiting on AI Endpoints

- [x] 5.1 Add `builder.Services.AddRateLimiter(...)` in `Program.cs` using a fixed-window policy named `"ai-limit"`: 10 permits per 60-second window, keyed on `RemoteIpAddress`, returning 429 on rejection
- [x] 5.2 Call `app.UseRateLimiter()` in the middleware pipeline after `app.UseCors()`
- [x] 5.3 Add `.RequireRateLimiting("ai-limit")` to the `/api/ai` route group in `AiEndpoints`
- [x] 5.4 Verify that non-AI endpoints (`/api/books`, `/api/loans`) are NOT assigned the rate limiting policy

## 6. UpdateBookValidator

- [x] 6.1 Add `UpdateBookValidator : AbstractValidator<UpdateBookRequest>` in `Endpoints.cs` with rules: `Title` max 300 when not null; `Author` max 200 when not null; `Isbn` max 20 when not null; `PublishedYear` between 1000 and current year + 1 when not null
- [x] 6.2 Inject `IValidator<UpdateBookRequest>` in the `PUT /{id:guid}` handler and call `ValidateAsync`, returning `Results.ValidationProblem(...)` on failure
- [x] 6.3 Register `UpdateBookValidator` — confirm Carter/FluentValidation auto-registration picks it up or add explicit `builder.Services.AddScoped<IValidator<UpdateBookRequest>, UpdateBookValidator>()`

## 7. Backend Test Project

- [x] 7.1 Create `backend/LibraMS.Api.Tests/LibraMS.Api.Tests.csproj` targeting `net8.0` with references to xUnit, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `Npgsql`, `Dapper`, and a project reference to `LibraMS.Api`
- [x] 7.2 Add `LibraMS.Api.Tests` to `Library assigment.sln`
- [x] 7.3 Create `backend/LibraMS.Api.Tests/Fixtures/TestDbFixture.cs` — reads `TEST_DB_CONNECTION_STRING` env var (skips collection if absent) and provides a shared `NpgsqlConnection`
- [x] 7.4 Create `backend/LibraMS.Api.Tests/Repositories/BookRepositoryTests.cs` with tests: `SearchAsync_NoFilters_ReturnsPaged`, `SearchAsync_WithQuery_FiltersResults`, `SearchAsync_WithStatusAvailable_ReturnsOnlyAvailable`, `CreateAsync_InsertAndReturns`, `DeleteAsync_RemovesBook`
- [x] 7.5 Create `backend/LibraMS.Api.Tests/Repositories/LoanRepositoryTests.cs` with tests: `CheckOutAsync_AvailableBook_CreatesLoan`, `CheckOutAsync_CheckedOutBook_ReturnsNull`, `CheckInAsync_ActiveLoan_ReturnsLoanAndFreesBook`
- [x] 7.6 Create `backend/LibraMS.Api.Tests/Endpoints/BooksEndpointTests.cs` using `WebApplicationFactory<Program>` with tests: `GetBooks_ReturnsOk`, `PostBook_Unauthenticated_Returns401`, `GetHealth_ReturnsOk`
- [x] 7.7 Run `dotnet test backend/` and confirm all tests pass (or skip gracefully when `TEST_DB_CONNECTION_STRING` is absent)

## 8. Frontend Test Setup

- [x] 8.1 Add dev dependencies to `frontend/package.json`: `vitest`, `@vitest/ui`, `@testing-library/react`, `@testing-library/jest-dom`, `@testing-library/user-event`, `jsdom`
- [x] 8.2 Add `test` script to `frontend/package.json`: `"test": "vitest run"`
- [x] 8.3 Add `test` block to `frontend/vite.config.ts`: `environment: "jsdom"`, `globals: true`, `setupFiles: ["./src/test/setup.ts"]`
- [x] 8.4 Create `frontend/src/test/setup.ts` importing `@testing-library/jest-dom`
- [x] 8.5 Create `frontend/src/test/LoginPage.test.tsx` — renders `LoginPage` and asserts a sign-in button is visible
- [x] 8.6 Create `frontend/src/test/BookFormModal.test.tsx` — tests empty-title validation error and successful submit callback
- [x] 8.7 Create `frontend/src/test/useAuth.test.tsx` — tests null user when no session and user object when session exists (mock Supabase client)
- [x] 8.8 Run `npm test` from `frontend/` and confirm all tests pass

## 9. CI/CD — Add Test Steps

- [x] 9.1 In `.github/workflows/deploy-backend.yml`, add a step before the Docker build: `dotnet test backend/ --no-build` (with a `TEST_DB_CONNECTION_STRING` secret injected as env var; step is skipped if secret is absent)
- [x] 9.2 In `.github/workflows/deploy-frontend.yml`, add a step before the build: `npm ci && npm test` run from `frontend/`

## 10. Documentation

- [x] 10.1 In `README.md`, replace the two placeholder live demo URLs (lines 9–10) with the real deployed Azure URLs once known, or leave a clear `TODO: fill after first deploy` note replacing the italicized placeholder text
- [x] 10.2 Add a **Overdue Loan Automation** section to `README.md` documenting how to schedule `mark_overdue_loans()` in Supabase: navigate to **Database → Extensions** → enable `pg_cron`, then run `SELECT cron.schedule('mark-overdue', '0 * * * *', $$SELECT mark_overdue_loans()$$);` in the SQL Editor
- [x] 10.3 Add a note to the README **F1 cold-start note** section that `/health` can be used as a warm-up probe URL

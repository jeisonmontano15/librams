## 1. Startup Blockers

- [ ] 1.1 Create `backend/LibraMS.Api/Data/UserRepository.cs` with `IUserRepository` interface defining `GetByIdAsync(Guid id)` and `EnsureExistsAsync(Guid id, string email, string? displayName)`
- [ ] 1.2 Implement `UserRepository` using Dapper — `GetByIdAsync` queries `public.library_users WHERE id = @id`; `EnsureExistsAsync` uses `INSERT ... ON CONFLICT (id) DO NOTHING`
- [ ] 1.3 Verify `Program.cs` line 49 already registers `IUserRepository, UserRepository` — confirm no further DI changes needed
- [ ] 1.4 Create `frontend/tsconfig.json` with `target: ES2020`, `lib: ["ES2020","DOM","DOM.Iterable"]`, `module: ESNext`, `moduleResolution: bundler`, `jsx: react-jsx`, `strict: true`, `paths` empty, referencing the `frontend/src` directory
- [ ] 1.5 Run `npm run build` from `frontend/` and confirm it exits without errors

## 2. Bug Fix — BookStatus Serialization

- [ ] 2.1 In `BookRepository.cs`, replace both `.ToString().ToLower().Replace("_","")` calls (lines ~41 and ~107) with an explicit switch expression: `BookStatus.Available => "available"`, `BookStatus.CheckedOut => "checked_out"`
- [ ] 2.2 Verify that `SearchAsync` status filter and `SetStatusAsync` both use the corrected mapping
- [ ] 2.3 Manually test checkout flow locally: borrow a book and confirm `books.status` is `"checked_out"` in the Supabase table editor

## 3. .gitignore

- [ ] 3.1 Create `.gitignore` at the repository root covering: `**/bin/`, `**/obj/`, `node_modules/`, `dist/`, `.env.local`, `*.user`, `*.suo`, `.DS_Store`, `*.log`
- [ ] 3.2 Run `git rm -r --cached backend/LibraMS.Api/obj/` to stop tracking the already-committed `obj/` artifacts

## 4. Health Check Endpoint

- [ ] 4.1 In `Endpoints.cs` (or a new `HealthEndpoints.cs`), add a Carter module that maps `GET /health` returning `Results.Ok(new { status = "ok" })` with no authorization requirement
- [ ] 4.2 Confirm `GET http://localhost:5000/health` returns `200 {"status":"ok"}` with no JWT

## 5. Rate Limiting on AI Endpoints

- [ ] 5.1 Add `builder.Services.AddRateLimiter(...)` in `Program.cs` using a fixed-window policy named `"ai-limit"`: 10 permits per 60-second window, keyed on `RemoteIpAddress`, returning 429 on rejection
- [ ] 5.2 Call `app.UseRateLimiter()` in the middleware pipeline after `app.UseCors()`
- [ ] 5.3 Add `.RequireRateLimiting("ai-limit")` to the `/api/ai` route group in `AiEndpoints`
- [ ] 5.4 Verify that non-AI endpoints (`/api/books`, `/api/loans`) are NOT assigned the rate limiting policy

## 6. UpdateBookValidator

- [ ] 6.1 Add `UpdateBookValidator : AbstractValidator<UpdateBookRequest>` in `Endpoints.cs` with rules: `Title` max 300 when not null; `Author` max 200 when not null; `Isbn` max 20 when not null; `PublishedYear` between 1000 and current year + 1 when not null
- [ ] 6.2 Inject `IValidator<UpdateBookRequest>` in the `PUT /{id:guid}` handler and call `ValidateAsync`, returning `Results.ValidationProblem(...)` on failure
- [ ] 6.3 Register `UpdateBookValidator` — confirm Carter/FluentValidation auto-registration picks it up or add explicit `builder.Services.AddScoped<IValidator<UpdateBookRequest>, UpdateBookValidator>()`

## 7. Backend Test Project

- [ ] 7.1 Create `backend/LibraMS.Api.Tests/LibraMS.Api.Tests.csproj` targeting `net8.0` with references to xUnit, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `Npgsql`, `Dapper`, and a project reference to `LibraMS.Api`
- [ ] 7.2 Add `LibraMS.Api.Tests` to `Library assigment.sln`
- [ ] 7.3 Create `backend/LibraMS.Api.Tests/Fixtures/TestDbFixture.cs` — reads `TEST_DB_CONNECTION_STRING` env var (skips collection if absent) and provides a shared `NpgsqlConnection`
- [ ] 7.4 Create `backend/LibraMS.Api.Tests/Repositories/BookRepositoryTests.cs` with tests: `SearchAsync_NoFilters_ReturnsPaged`, `SearchAsync_WithQuery_FiltersResults`, `SearchAsync_WithStatusAvailable_ReturnsOnlyAvailable`, `CreateAsync_InsertAndReturns`, `DeleteAsync_RemovesBook`
- [ ] 7.5 Create `backend/LibraMS.Api.Tests/Repositories/LoanRepositoryTests.cs` with tests: `CheckOutAsync_AvailableBook_CreatesLoan`, `CheckOutAsync_CheckedOutBook_ReturnsNull`, `CheckInAsync_ActiveLoan_ReturnsLoanAndFreesBook`
- [ ] 7.6 Create `backend/LibraMS.Api.Tests/Endpoints/BooksEndpointTests.cs` using `WebApplicationFactory<Program>` with tests: `GetBooks_ReturnsOk`, `PostBook_Unauthenticated_Returns401`, `GetHealth_ReturnsOk`
- [ ] 7.7 Run `dotnet test backend/` and confirm all tests pass (or skip gracefully when `TEST_DB_CONNECTION_STRING` is absent)

## 8. Frontend Test Setup

- [ ] 8.1 Add dev dependencies to `frontend/package.json`: `vitest`, `@vitest/ui`, `@testing-library/react`, `@testing-library/jest-dom`, `@testing-library/user-event`, `jsdom`
- [ ] 8.2 Add `test` script to `frontend/package.json`: `"test": "vitest run"`
- [ ] 8.3 Add `test` block to `frontend/vite.config.ts`: `environment: "jsdom"`, `globals: true`, `setupFiles: ["./src/test/setup.ts"]`
- [ ] 8.4 Create `frontend/src/test/setup.ts` importing `@testing-library/jest-dom`
- [ ] 8.5 Create `frontend/src/test/LoginPage.test.tsx` — renders `LoginPage` and asserts a sign-in button is visible
- [ ] 8.6 Create `frontend/src/test/BookFormModal.test.tsx` — tests empty-title validation error and successful submit callback
- [ ] 8.7 Create `frontend/src/test/useAuth.test.tsx` — tests null user when no session and user object when session exists (mock Supabase client)
- [ ] 8.8 Run `npm test` from `frontend/` and confirm all tests pass

## 9. CI/CD — Add Test Steps

- [ ] 9.1 In `.github/workflows/deploy-backend.yml`, add a step before the Docker build: `dotnet test backend/ --no-build` (with a `TEST_DB_CONNECTION_STRING` secret injected as env var; step is skipped if secret is absent)
- [ ] 9.2 In `.github/workflows/deploy-frontend.yml`, add a step before the build: `npm ci && npm test` run from `frontend/`

## 10. Documentation

- [ ] 10.1 In `README.md`, replace the two placeholder live demo URLs (lines 9–10) with the real deployed Azure URLs once known, or leave a clear `TODO: fill after first deploy` note replacing the italicized placeholder text
- [ ] 10.2 Add a **Overdue Loan Automation** section to `README.md` documenting how to schedule `mark_overdue_loans()` in Supabase: navigate to **Database → Extensions** → enable `pg_cron`, then run `SELECT cron.schedule('mark-overdue', '0 * * * *', $$SELECT mark_overdue_loans()$$);` in the SQL Editor
- [ ] 10.3 Add a note to the README **F1 cold-start note** section that `/health` can be used as a warm-up probe URL

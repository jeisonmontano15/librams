# Tasks: LibraMS — Bug Fixes

## BUG-1 — Missing `loans.user_email` column (Critical)

- [x] Add `ALTER TABLE public.loans ADD COLUMN IF NOT EXISTS user_email TEXT;` to `schema.sql` beside the `loans` table definition
- [x] Verify the full `schema.sql` runs clean twice in a row against an empty database (idempotency) — `Schema_AppliedTwice_IsIdempotent` passes against local PostgreSQL 16
- [x] Verify checkout succeeds end to end against a database provisioned only from `schema.sql` — `CheckOut_AgainstSchemaProvisionedDatabase_PersistsUserEmail` passes
- [x] Confirm a loan row with a null `user_email` deserialises to an empty string without error — `Loan_WithNullUserEmail_MapsToEmptyString` passes; `Loan.UserEmail` is `string` with a `""` initialiser (`Models.cs:25`)

## BUG-2 — Check-in is not authorised (Critical)

- [x] Change `ILoanRepository.CheckInAsync` to `CheckInAsync(Guid loanId, Guid userId, bool isLibrarian)`
- [x] Scope the check-in UPDATE with `AND (@isLibrarian OR user_id = @userId)`
- [x] Read caller id and librarian role in the check-in endpoint and pass them through — `IsLibrarian` reads the `user_role` claim injected by `RoleEnrichmentMiddleware`, matching the `LibrarianOnly` policy
- [x] Confirm a non-matching loan returns `404` (not `403`), so other users' loan IDs are not disclosed — the repository returns null for both "not found" and "not yours", and the endpoint maps null to `Results.NotFound()`
- [x] Verify the `AdminLoans` librarian check-in flow still works — the request/response shape is unchanged, so `useCheckIn` needs no edit; `CheckInAsync_LibrarianReturningAnotherMembersLoan_Succeeds` covers the repository path

## BUG-3 — Nonexistent book returns 409 instead of 404

- [x] Introduce a checkout result type distinguishing `Success`, `NotFound`, and `Unavailable` — `CheckOutOutcome` enum and `CheckOutResult` record (`Models.cs:35-47`); `ILoanRepository.CheckOutAsync` now returns `CheckOutResult`
- [x] Return `NotFound` when the book row does not exist, `Unavailable` when its status is not `available` — the availability query selects `id, status` so a missing row is distinguishable from a row with a null status (`LoanRepository.cs:39-42`)
- [x] Map `NotFound` to `404` and `Unavailable` to `409` in the checkout endpoint — `Endpoints.cs:90-96`; the `409` payload `{"error":"Book is not available for checkout."}` is unchanged
- [x] Confirm the `SELECT … FOR UPDATE` row lock is preserved for books that exist — still issued inside the transaction before the loan INSERT (`LoanRepository.cs:40`)

## BUG-4 — Genre filter matches substrings

- [x] Remove the `%…%` wrapping from the genre parameter in `BookRepository.SearchAsync`, keeping `ILIKE` (`BookRepository.cs:43-46`)
- [x] Verify filtering `Fiction` excludes `Non-Fiction` and `Science Fiction` — `SearchAsync_GenreFilter_ExcludesSubstringMatches` passes
- [x] Verify filtering `Science Fiction` still returns that genre — `SearchAsync_CompoundGenreFilter_ReturnsThatGenre` passes; case-insensitivity retained per `SearchAsync_GenreFilter_IsCaseInsensitive`

## BUG-5 — Optional book fields cannot be cleared

- [x] Replace `COALESCE` with clear-on-empty `CASE` expressions for `genre`, `description`, and `cover_url` (`BookRepository.cs:95-114`)
- [x] Leave `title` and `author` on plain `COALESCE` (required fields, not clearable)
- [x] Leave `isbn` and `published_year` on plain `COALESCE`
- [x] Confirm omitting a field still preserves it, distinct from clearing it — `UpdateAsync_OmittedDescription_PreservesIt` clears nothing while `UpdateAsync_EmptyDescription_ClearsIt` clears only the field submitted as empty; `UpdateAsync_RequiredFields_AreNotClearedByEmptyValues` covers the untouched fields

## BUG-6 — AI rate limit is global rather than per caller

- [x] Replace `AddFixedWindowLimiter("ai-limit", …)` with `AddPolicy` using a partitioned limiter — `Program.cs:114`; the partition itself lives in `AiRateLimitPolicy.GetPartition` (`AiRateLimitPolicy.cs`) because `RateLimiterOptions` does not expose registered policies for inspection, leaving the behaviour untestable when written inline
- [x] Key the partition on the authenticated user id, falling back to remote IP — `RateLimitPartitionKey.For` (`RateLimitPartitionKey.cs`), prefixing `user:`/`ip:` so a user id can never collide with an address; final fallback is a shared `anonymous` bucket
- [x] Preserve the existing 10 requests / 60 seconds budget and the `429` rejection payload — `PermitLimit`/`Window` unchanged and asserted by `AiLimit_PreservesTheDocumentedBudget`; `OnRejected` and `RejectionStatusCode` untouched
- [x] Verify two distinct callers each receive an independent quota — `AiLimit_OneCallerExhaustingQuota_DoesNotAffectAnother`, with both callers on the same IP so the test fails if partitioning falls back to address

## BUG-7 — CORS wildcard origins are inert

- [x] Add `.SetIsOriginAllowedToAllowWildcardSubdomains()` to the CORS policy — `Program.cs:129-138`
- [x] Verify a wildcard-matching origin is permitted and an unrelated origin is refused — `CorsPolicyTests` drives the real configured policy resolved from the running host; `https://vercel.app.evil.com` and a wrong-scheme origin are both refused

## BUG-8 — JWKS fetched per validation on a fresh HttpClient

- [x] Cache the resolved signing keys with a periodic refresh instead of fetching per validation — `SupabaseSigningKeys` (`SupabaseSigningKeys.cs`), a one-hour `RefreshInterval` behind a `SemaphoreSlim` gate; extracted from `Program.cs` for the same reason as `AiRateLimitPolicy` — the resolver lambda was unreachable from tests
- [x] Remove the per-validation `new HttpClient()` and the blocking `.GetAwaiter().GetResult()` — one process-lifetime `HttpClient` (`Program.cs:29-33`); the blocking call now happens once per refresh rather than once per request, since `IssuerSigningKeyResolver` is a synchronous delegate and cannot be awaited
- [x] Preserve the manual `ECDsa` construction for Supabase's `key_ops: ["verify"]` keys — moved verbatim into `SupabaseSigningKeys.ParseKeys`; `Get_ParsesSupabaseVerifyOnlyEcKeys` pins it against a `key_ops: ["verify"]` JWKS
- [x] Verify repeated validations trigger a single key fetch — `SigningKeyCacheTests.RepeatedValidations_TriggerASingleFetch`; `Get_ConcurrentCallers_StillFetchOnce` covers the gate and `Get_WhenARefreshFails_KeepsServingTheLastKnownGoodKeys` covers a JWKS outage not rejecting valid tokens

## BUG-9 — Database tests silently pass when unconfigured

- [x] Determine whether the project is on xUnit v2 or v3 before choosing the skip mechanism — xUnit **2.9.2** (`LibraMS.Api.Tests.csproj`), so `Assert.Skip` is unavailable; added `Xunit.SkippableFact` 1.4.13 as the design anticipated
- [x] Replace the silent `if (!TestDbFixture.IsAvailable) return;` guards with a visible skip — `TestDbFixture.SkipIfUnavailable()` wraps `Skip.IfNot` with the reason; all 22 guards across `BookRepositoryTests`, `LoanRepositoryTests`, and `SchemaRegressionTests` replaced, and their `[Fact]` attributes changed to `[SkippableFact]` (the skip is raised as an exception, so a plain `[Fact]` would report it as failed)
- [x] Verify the suite reports skipped — not passed — when `TEST_DB_CONNECTION_STRING` is unset — with the variable unset: `Passed: 22, Skipped: 22, Total: 44`; the same 22 previously counted as passed

## Regression tests

- [x] BUG-1: checkout against a schema-provisioned database persists `user_email` — `SchemaRegressionTests.cs`, verified failing before the fix (`42703: column "user_email" does not exist`) and passing after
- [x] BUG-2: member cannot check in another member's loan; librarian can — `LoanRepositoryTests.CheckInAsync_LoanBelongingToAnotherMember_ReturnsNullAndLeavesLoanOutstanding` and `CheckInAsync_LibrarianReturningAnotherMembersLoan_Succeeds`, verified failing before the fix and passing after
- [x] BUG-3: unknown book id returns `404`; borrowed book returns `409` — `LoanRepositoryTests.CheckOutAsync_UnknownBookId_ReturnsNotFound` and `CheckOutAsync_CheckedOutBook_ReturnsUnavailable`, verified failing before the fix (`NotFound` expected, `Unavailable` actual) and passing after
- [x] BUG-4: genre filter excludes substring matches — `BookRepositoryTests.SearchAsync_GenreFilter_ExcludesSubstringMatches`, verified failing before the fix (`Assert.DoesNotContain() Failure: Filter matched in collection`) and passing after
- [x] BUG-5: empty description clears it; omitted description preserves it — `BookRepositoryTests.UpdateAsync_EmptyDescription_ClearsIt` and `UpdateAsync_EmptyGenreAndCoverUrl_ClearsThem`, verified failing before the fix (`Assert.Null() Failure: Value is not null`) and passing after
- [x] BUG-6: one caller's exhausted quota does not affect another caller — `RateLimitPartitionTests.AiLimit_OneCallerExhaustingQuota_DoesNotAffectAnother`, verified failing before the fix (with the partition key forced to a constant, caller B was refused) and passing after
- [x] BUG-7: wildcard subdomain origin is permitted by the CORS policy — `CorsPolicyTests.WildcardMatchingOrigin_IsAllowed`, verified failing before the fix (all three wildcard origins refused) and passing after
- [x] BUG-8: repeated token validations trigger a single JWKS fetch — `SigningKeyCacheTests.RepeatedValidations_TriggerASingleFetch`, verified failing before the fix (cache lookup removed to restore per-validation fetching: `Assert.Equal() Failure: Expected 1, Actual 50`, 3 of 5 failing) and passing after
- [x] Confirm each regression test fails before its fix and passes after — recorded per bug above; BUG-9 is the exception by nature, since its "test" is the suite's own reporting: with `TEST_DB_CONNECTION_STRING` unset the pre-fix suite reported `15 passed / 0 skipped` and the post-fix suite reports `22 passed / 22 skipped`

## Verification

- [x] `dotnet test` passes with `TEST_DB_CONNECTION_STRING` configured — `Failed: 0, Passed: 44, Skipped: 0, Total: 44` against local PostgreSQL 16
- [x] `npm test` passes in `frontend/` — `Test Files 3 passed (3), Tests 4 passed (4)`
- [x] Manual smoke test: sign in, search, checkout, check in, edit a book, clear a field — scripted for both local and production in [`SMOKE-TEST.md`](../../../SMOKE-TEST.md), each step naming the defect it guards and the pre-fix symptom; three checks (unknown-book `404`, non-owner check-in, librarian override) have no UI path and are given as `curl` calls. **Execution is left to a human** — it needs Google OAuth and two accounts, which cannot be driven from here; record the result in the guide's results table
- [x] `openspec validate librams-bug-fixes --strict` passes — `Change 'librams-bug-fixes' is valid`

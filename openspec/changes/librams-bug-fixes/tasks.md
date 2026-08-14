# Tasks: LibraMS — Bug Fixes

## BUG-1 — Missing `loans.user_email` column (Critical)

- [ ] Add `ALTER TABLE public.loans ADD COLUMN IF NOT EXISTS user_email TEXT;` to `schema.sql` beside the `loans` table definition
- [ ] Verify the full `schema.sql` runs clean twice in a row against an empty database (idempotency)
- [ ] Verify checkout succeeds end to end against a database provisioned only from `schema.sql`
- [ ] Confirm a loan row with a null `user_email` deserialises to an empty string without error

## BUG-2 — Check-in is not authorised (Critical)

- [ ] Change `ILoanRepository.CheckInAsync` to `CheckInAsync(Guid loanId, Guid userId, bool isLibrarian)`
- [ ] Scope the check-in UPDATE with `AND (@isLibrarian OR user_id = @userId)`
- [ ] Read caller id and librarian role in the check-in endpoint and pass them through
- [ ] Confirm a non-matching loan returns `404` (not `403`), so other users' loan IDs are not disclosed
- [ ] Verify the `AdminLoans` librarian check-in flow still works

## BUG-3 — Nonexistent book returns 409 instead of 404

- [ ] Introduce a checkout result type distinguishing `Success`, `NotFound`, and `Unavailable`
- [ ] Return `NotFound` when the book row does not exist, `Unavailable` when its status is not `available`
- [ ] Map `NotFound` to `404` and `Unavailable` to `409` in the checkout endpoint
- [ ] Confirm the `SELECT … FOR UPDATE` row lock is preserved for books that exist

## BUG-4 — Genre filter matches substrings

- [ ] Remove the `%…%` wrapping from the genre parameter in `BookRepository.SearchAsync`, keeping `ILIKE`
- [ ] Verify filtering `Fiction` excludes `Non-Fiction` and `Science Fiction`
- [ ] Verify filtering `Science Fiction` still returns that genre

## BUG-5 — Optional book fields cannot be cleared

- [ ] Replace `COALESCE` with clear-on-empty `CASE` expressions for `genre`, `description`, and `cover_url`
- [ ] Leave `title` and `author` on plain `COALESCE` (required fields, not clearable)
- [ ] Leave `isbn` and `published_year` on plain `COALESCE`
- [ ] Confirm omitting a field still preserves it, distinct from clearing it

## BUG-6 — AI rate limit is global rather than per caller

- [ ] Replace `AddFixedWindowLimiter("ai-limit", …)` with `AddPolicy` using a partitioned limiter
- [ ] Key the partition on the authenticated user id, falling back to remote IP
- [ ] Preserve the existing 10 requests / 60 seconds budget and the `429` rejection payload
- [ ] Verify two distinct callers each receive an independent quota

## BUG-7 — CORS wildcard origins are inert

- [ ] Add `.SetIsOriginAllowedToAllowWildcardSubdomains()` to the CORS policy
- [ ] Verify a wildcard-matching origin is permitted and an unrelated origin is refused

## BUG-8 — JWKS fetched per validation on a fresh HttpClient

- [ ] Cache the resolved signing keys with a periodic refresh instead of fetching per validation
- [ ] Remove the per-validation `new HttpClient()` and the blocking `.GetAwaiter().GetResult()`
- [ ] Preserve the manual `ECDsa` construction for Supabase's `key_ops: ["verify"]` keys
- [ ] Verify repeated validations trigger a single key fetch

## BUG-9 — Database tests silently pass when unconfigured

- [ ] Determine whether the project is on xUnit v2 or v3 before choosing the skip mechanism
- [ ] Replace the silent `if (!TestDbFixture.IsAvailable) return;` guards with a visible skip
- [ ] Verify the suite reports skipped — not passed — when `TEST_DB_CONNECTION_STRING` is unset

## Regression tests

- [ ] BUG-1: checkout against a schema-provisioned database persists `user_email`
- [ ] BUG-2: member cannot check in another member's loan; librarian can
- [ ] BUG-3: unknown book id returns `404`; borrowed book returns `409`
- [ ] BUG-4: genre filter excludes substring matches
- [ ] BUG-5: empty description clears it; omitted description preserves it
- [ ] BUG-6: one caller's exhausted quota does not affect another caller
- [ ] BUG-7: wildcard subdomain origin is permitted by the CORS policy
- [ ] BUG-8: repeated token validations trigger a single JWKS fetch
- [ ] Confirm each regression test fails before its fix and passes after

## Verification

- [ ] `dotnet test` passes with `TEST_DB_CONNECTION_STRING` configured
- [ ] `npm test` passes in `frontend/`
- [ ] Manual smoke test: sign in, search, checkout, check in, edit a book, clear a field
- [ ] `openspec validate librams-bug-fixes --strict` passes

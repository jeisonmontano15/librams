# Design: LibraMS — Bug Fixes

Nine defects, ordered by severity. Each section states the defect, its observable
effect, and the intended fix.

---

## BUG-1 — `loans.user_email` is written but never created (Critical)

**Where:** `backend/LibraMS.Api/Data/LoanRepository.cs:39`, `Data/schema.sql:60-69`

`CheckOutAsync` inserts into `book_id, user_id, user_email`, but the `loans` table
declares only `id, book_id, user_id, checked_out_at, due_date, returned_at, status,
created_at`. The string `user_email` appears exactly once in the entire repository — in
that INSERT.

**Effect:** every checkout fails with `42703: column "user_email" does not exist`. The
global exception handler converts it into an opaque
`500 {"error":"An unexpected error occurred."}`.

This was first written up as schema drift — committed schema behind a live database that
had the column added out-of-band. **That was wrong.** Checking the live database on
2026-08-14 found no `user_email` column in any schema, and `public.loans` contained zero
rows: the feature had never worked in production either. The severity is therefore higher
than "fails on clean setup" — checkout was broken for every user, everywhere.

**Fix:** add the column to the canonical schema, as nullable so it is safe against the
live database and any existing rows:

```sql
ALTER TABLE public.loans ADD COLUMN IF NOT EXISTS user_email TEXT;
```

Place it in `schema.sql` next to the `loans` table definition (using `ADD COLUMN IF NOT
EXISTS` so the whole script stays idempotent, consistent with the `CREATE TABLE IF NOT
EXISTS` style already used). `Loan.UserEmail` is non-nullable in C# with a `""` default,
so Dapper maps a null column to empty string without error — no model change needed.

**Why not drop the column from the INSERT instead:** `user_email` is genuinely useful.
It denormalises the borrower's email onto the loan so the librarian views can show who
holds a book without joining `library_users`, and it preserves the address as it was at
checkout time. Keep the write, fix the schema.

---

## BUG-2 — Check-in is not authorised (Critical)

**Where:** `backend/LibraMS.Api/Endpoints/Endpoints.cs:97-103`,
`Data/LoanRepository.cs:59-83`

The endpoint accepts `HttpContext ctx` but never reads the caller from it, and
`CheckInAsync(loanId)` filters only on loan ID. Any authenticated user can return any
loan by ID.

**Effect:** a member can return another member's book, making it immediately available
for checkout. The book leaves the borrower's active loans without their action.

**Fix:** pass the caller's identity and role into the repository and scope the UPDATE:

```csharp
Task<Loan?> CheckInAsync(Guid loanId, Guid userId, bool isLibrarian);
```

```sql
UPDATE public.loans SET status = 'returned', returned_at = NOW()
WHERE id = @loanId AND status != 'returned'
  AND (@isLibrarian OR user_id = @userId)
RETURNING *
```

Librarians retain unrestricted check-in — returns happen at the desk, and the
`AdminLoans` page depends on it.

**Response code:** keep `404` when no row matches. Distinguishing "not found" from "not
yours" via `403` would confirm the existence of another user's loan ID, so a uniform
`404` is the better choice here.

---

## BUG-3 — Nonexistent book reports `409` instead of `404` (Moderate)

**Where:** `Data/LoanRepository.cs:33-35`, `Endpoints/Endpoints.cs:90-93`

`QuerySingleOrDefaultAsync<string>` returns null for a missing book; `null !=
"available"` is true, so `CheckOutAsync` returns null — the same signal it uses for
"already checked out". The endpoint maps that to `409 Conflict, "Book is not available
for checkout."`

**Effect:** checking out a deleted or mistyped book ID reports a conflict rather than
not-found, misleading clients.

**Fix:** distinguish the two cases. Return a small result enum from the repository
(`Success` / `NotFound` / `Unavailable`) rather than a nullable `Loan`, and map
`NotFound → 404`, `Unavailable → 409`. Note the `FOR UPDATE` row lock is correct and
must be preserved for books that do exist.

**As implemented:** the enum alone is not sufficient — the endpoint still needs the loan
on the success path — so the repository returns a `CheckOutResult(CheckOutOutcome, Loan?)`
record carrying both. The availability query also had to change: `SELECT status … FOR
UPDATE` answers null both for a missing row and for a row whose `status` is null, so it
now selects `id, status` and treats a null row — not a null status — as `NotFound`.

---

## BUG-4 — Genre filter matches substrings (Moderate)

**Where:** `Data/BookRepository.cs:42-43`

```csharp
conditions.Add("genre ILIKE @genre");
parameters.Add("genre", $"%{req.Genre}%");
```

**Effect:** filtering by `Fiction` also returns `Non-Fiction` and `Science Fiction`. The
genre dropdown is populated from `GetGenresAsync()`, which returns exact stored values,
so the wildcards serve no purpose and actively corrupt the filter.

**Fix:** drop the wildcards, keeping `ILIKE` for case-insensitivity:

```csharp
parameters.Add("genre", req.Genre);
```

---

## BUG-5 — Optional book fields cannot be cleared (Moderate)

**Where:** `Data/BookRepository.cs:93-99`

Every field uses `COALESCE(@Field, field)`, which correctly implements "omitted means
unchanged" but makes an explicit null indistinguishable from an absent field.

**Effect:** once a description, genre, or cover URL is set, a librarian can never remove
it through the UI.

**Fix:** scope this narrowly. For the three fields where clearing is meaningful —
`genre`, `description`, `cover_url` — treat the empty string as an explicit clear:

```sql
genre = CASE WHEN @Genre IS NULL THEN genre
             WHEN @Genre = ''    THEN NULL
             ELSE @Genre END
```

`title` and `author` are required and keep plain `COALESCE`. `isbn` and
`published_year` keep `COALESCE` as well; clearing them is not a use case the UI
offers. A general JSON Patch layer is deliberately out of scope.

---

## BUG-6 — AI rate limit is global, not per client (Moderate)

**Where:** `Program.cs:112-127`

`AddFixedWindowLimiter("ai-limit", …)` creates a single unpartitioned bucket of 10
requests per minute shared by every caller.

**Effect:** one user exhausts the AI quota for all users. This also contradicts the
project's own already-archived spec, `openspec/specs/api-rate-limiting/spec.md`, which
states the limit is "per IP address".

**Fix:** partition per caller using `AddPolicy` with a partition key — the authenticated
user id where present, falling back to remote IP:

```csharp
options.AddPolicy("ai-limit", ctx =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.User.FindFirst("sub")?.Value
                      ?? ctx.Connection.RemoteIpAddress?.ToString()
                      ?? "anonymous",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(60),
            QueueLimit = 0,
        }));
```

Partitioning on user id rather than IP alone is the better fit here, since the AI
endpoints all require authentication and shared-NAT users would otherwise collide. The
spec is updated to say "per authenticated user, falling back to IP" to match.

**As implemented:** the partition is not an inline lambda. `RateLimiterOptions` exposes
no way to read back a registered policy, so a lambda in `Program.cs` would have left the
partitioning behaviour — the whole point of the fix — unreachable from tests. The policy
therefore lives in `AiRateLimitPolicy` (budget plus partition factory) over
`RateLimitPartitionKey.For` (the key rule), and `Program.cs` registers it with
`options.AddPolicy(AiRateLimitPolicy.Name, AiRateLimitPolicy.GetPartition)`.

Two details beyond the sketch: keys are prefixed (`user:` / `ip:`) so a user id can never
collide with an address, and the regression test puts both callers on the *same* IP, so
it fails if partitioning ever silently degrades to IP-only.

---

## BUG-7 — CORS wildcard origins are inert (Minor)

**Where:** `Program.cs:129-139`

`WithOrigins("https://*.vercel.app", "https://*.azurestaticapps.net")` performs exact
string comparison; the wildcards never match a real origin.

**Effect:** currently masked, because deployment uses the explicitly configured
`Frontend:Url`. The entries are dead configuration that would mislead the next person
who relies on them.

**Fix:** add `.SetIsOriginAllowedToAllowWildcardSubdomains()` to the policy, which makes
the existing wildcard entries behave as intended.

---

## BUG-8 — JWKS fetched per validation on a fresh `HttpClient` (Minor)

**Where:** `Program.cs:43-78`

`IssuerSigningKeyResolver` constructs `new HttpClient()` inside the resolver and blocks
on `.GetAwaiter().GetResult()` for every token validation.

**Effect:** a network round trip to Supabase on every authenticated request, socket
exhaustion risk under load, and a blocking call on a request thread. Functionally
correct but wasteful; acceptable at assignment scale, worth fixing cheaply.

**Fix:** resolve the key set once and cache it in a `static Lazy<IReadOnlyList<
SecurityKey>>` with a short refresh interval, so normal operation performs no network
call. Keep the manual `ECDsa` construction — it exists because `GetSigningKeys()` skips
Supabase's `key_ops: ["verify"]` keys, as the existing comment documents.

**As implemented:** not a `Lazy<>`. `Lazy` computes once and never refreshes, which would
mean a key rotation could only be picked up by restarting the process. The cache is
instead a `SupabaseSigningKeys` instance holding the key set behind an expiry timestamp
and a `SemaphoreSlim`, refreshing on the first `Get()` past a one-hour interval. As with
BUG-6, it lives in its own class rather than inline in `Program.cs` because a resolver
lambda is unreachable from tests.

Two details beyond the sketch. The blocking `.GetAwaiter().GetResult()` could not be
removed outright — `IssuerSigningKeyResolver` is a synchronous delegate, so there is
nothing to await into; what changed is that it now runs once per refresh instead of once
per request. And a failed refresh keeps serving the last known-good key set rather than
propagating: a transient JWKS outage would otherwise reject every valid token at once,
which is a worse failure than briefly serving slightly stale keys.

---

## BUG-9 — Loan tests silently pass when no database is configured (Minor)

**Where:** `LibraMS.Api.Tests/Fixtures/TestDbFixture.cs`, all repository tests

Every database test opens with `if (!TestDbFixture.IsAvailable) return;`, so without
`TEST_DB_CONNECTION_STRING` the suite reports green while asserting nothing.

**Effect:** this is why BUG-1 was never caught — `CheckOutAsync_AvailableBook_CreatesLoan`
genuinely exercises the broken INSERT and would fail against a clean schema, but it
never ran. A green suite that skipped its own coverage is worse than a red one.

Confirmed in practice on 2026-08-14: with no `TEST_DB_CONNECTION_STRING` the suite
reported `15 passed` while the four new BUG-1 regression tests asserted nothing. Pointed
at a real database, two of them failed immediately with the genuine `42703`. This bug
masks every other database-backed test in the change, so it is worth fixing early
despite its "minor" severity.

**Fix:** replace the silent `return` with a skip that is visible in test output, so
skipped coverage is reported rather than hidden:

```csharp
Assert.Skip.Unless(TestDbFixture.IsAvailable, "TEST_DB_CONNECTION_STRING not set");
```

(xUnit v3 `Assert.Skip`; if the project stays on xUnit v2, use a `SkippableFact` from
`Xunit.SkippableFact` instead.) Verify which is in use before implementing.

**As implemented:** the project is on xUnit **2.9.2**, so `Assert.Skip` does not exist and
the `Xunit.SkippableFact` (1.4.13) path applies. The guard is wrapped as
`TestDbFixture.SkipIfUnavailable()` so the reason string stays in one place, and all 22
guarded tests moved from `[Fact]` to `[SkippableFact]` — the skip is raised as an
exception, so a plain `[Fact]` would report it as a *failure* rather than a skip.

Verified both ways: with `TEST_DB_CONNECTION_STRING` unset the suite now reports
`22 passed / 22 skipped` where it previously reported those same 22 as passed; with the
variable set it reports `44 passed / 0 skipped`.

---

## Found during implementation (not in the original nine)

### BUG-10 — `BookStatus` cannot be read back from the database (Critical)

**Where:** `Models.cs:18`, `Data/BookRepository.cs:70-75` (and every `SELECT *` on `books`)

`books.status` stores `checked_out`, but the enum member is `CheckedOut`. Dapper's
`MatchNamesWithUnderscores` normalises *column names*, not *enum values* — those go through
`Enum.Parse`, which does not strip underscores. Any read of a book whose status is
`checked_out` therefore throws:

```
System.Data.DataException : Error parsing column 8 (status=checked_out - String)
---- System.ArgumentException : Requested value 'checked_out' was not found.
```

`BookRepository` already hand-maps the enum on *writes* (`SetStatusAsync`, `SearchAsync`),
which is why the gap on reads went unnoticed.

**Effect:** `GET /api/books/{id}` for a borrowed book, and any listing that includes one,
fails with a `500`. Surfaced on 2026-08-14 by the BUG-2 regression test — the first test to
read a book while it was still checked out.

**Fix (proposed):** register a Dapper `SqlMapper.TypeHandler<BookStatus>` that maps
`available`/`checked_out` in both directions, and drop the ad-hoc `switch` expressions in
`BookRepository` in favour of it. Not implemented — outside the BUG-2 task set.

### BUG-11 — Test assembly does not share the API's Dapper configuration (Critical, fixed)

**Where:** `Program.cs:15`, `LibraMS.Api.Tests`

`DefaultTypeMap.MatchNamesWithUnderscores = true` is set in `Program.cs`, which never runs
under the test host — the repository tests construct repositories directly. Every
snake_case column (`book_id`, `user_id`, `returned_at`) silently mapped to its default, so
assertions compared against `Guid.Empty` rather than real data.

**Effect:** compounds BUG-9. Two committed tests
(`CheckOutAsync_AvailableBook_CreatesLoan`, `CheckInAsync_ActiveLoan_ReturnsLoanAndFreesBook`)
were failing on `master` for this reason, and it made the BUG-2 regression tests
unverifiable — they could not distinguish a real pass from unmapped zeros.

**Fix:** a `[ModuleInitializer]` in `LibraMS.Api.Tests/Fixtures/DapperSetup.cs` applies the
same global configuration for the test assembly. This is a stopgap: the duplication is the
underlying smell, and hoisting the configuration into `LibraMS.Api` so both hosts share one
source of truth would be the better end state.

---

## Testing strategy

Each fix gets a regression test that fails before it:

| Bug | Test |
|-----|------|
| 1 | Checkout against a schema built from `schema.sql` succeeds and persists `user_email` |
| 2 | Member A cannot check in member B's loan (`404`, loan unchanged); librarian can |
| 3 | Checkout of an unknown book id returns `404`; of a borrowed book returns `409` |
| 4 | Filtering `Fiction` excludes `Non-Fiction` and `Science Fiction` |
| 5 | Update with empty description clears it; update omitting description preserves it |
| 6 | Two distinct callers each get their own quota; one caller exceeding gets `429` |
| 7 | Origin `https://foo.vercel.app` is permitted by the CORS policy |
| 8 | Repeated validations trigger a single JWKS fetch |
| 9 | Suite reports skipped (not passed) when no test database is configured |

The BUG-1 test is the one that matters most: it must run against a database provisioned
from the committed `schema.sql`, since that is precisely the gap that let the defect
ship.

## Migration note for existing deployments

The live database did **not** have `user_email` — the original assumption that it did was
incorrect, so a migration step was genuinely required rather than optional.

Applied to the live database (project `xomlcnrubaiofmbxnovx`) on 2026-08-14:

```sql
ALTER TABLE public.loans ADD COLUMN IF NOT EXISTS user_email TEXT;
```

Verified afterwards: the column exists as nullable `text`, re-running the statement is a
no-op (`already exists, skipping`), and the `CheckOutAsync` INSERT succeeds against the
live schema. No existing rows needed backfilling — the table was empty, because no
checkout had ever succeeded.

Any other deployment provisioned from the previous schema picks the column up on the next
`schema.sql` run.

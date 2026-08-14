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

**Effect:** every checkout against a database provisioned from `schema.sql` fails with
`42703: column "user_email" does not exist`. The global exception handler converts it
into an opaque `500 {"error":"An unexpected error occurred."}`. The live database has
the column (added out-of-band), which is why the deployed app works and the committed
schema does not.

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

---

## BUG-9 — Loan tests silently pass when no database is configured (Minor)

**Where:** `LibraMS.Api.Tests/Fixtures/TestDbFixture.cs`, all repository tests

Every database test opens with `if (!TestDbFixture.IsAvailable) return;`, so without
`TEST_DB_CONNECTION_STRING` the suite reports green while asserting nothing.

**Effect:** this is why BUG-1 was never caught — `CheckOutAsync_AvailableBook_CreatesLoan`
genuinely exercises the broken INSERT and would fail against a clean schema, but it
never ran. A green suite that skipped its own coverage is worse than a red one.

**Fix:** replace the silent `return` with a skip that is visible in test output, so
skipped coverage is reported rather than hidden:

```csharp
Assert.Skip.Unless(TestDbFixture.IsAvailable, "TEST_DB_CONNECTION_STRING not set");
```

(xUnit v3 `Assert.Skip`; if the project stays on xUnit v2, use a `SkippableFact` from
`Xunit.SkippableFact` instead.) Verify which is in use before implementing.

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

The live database already has `user_email`. `ADD COLUMN IF NOT EXISTS` makes re-running
`schema.sql` a no-op there, so no separate migration step is required. Deployments
provisioned from the previous schema pick the column up on the next run.

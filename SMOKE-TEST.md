# Manual Smoke Test — LibraMS

A pass through the six flows the automated suite cannot reach: sign in, search,
checkout, check in, edit a book, clear a field. Runs identically against a local stack
and against production; the differences are collected in [Environments](#environments).

Every step names the defect it would catch. Seven of the nine bugs in
`librams-bug-fixes` are observable from the UI, and the checks below are ordered so a
failure points at one bug rather than a class of them.

**Time:** ~10 minutes. **You need:** two signed-in accounts — one librarian, one member
(see [Accounts](#accounts)).

---

## Environments

| | Local | Production |
|---|---|---|
| Frontend | `http://localhost:5173` | your Static Web App URL (`VITE_API_URL`'s sibling) |
| API | `http://localhost:5000` | your App Service URL |
| API docs | `http://localhost:5000/docs` | `<api>/docs` |
| Database | Supabase project, or local PostgreSQL | Supabase project (shared — see the warning) |

### Local — bring the stack up

Two terminals, from the repo root. Full first-time configuration is in
[README.md](README.md#running-locally); this assumes it is already done.

```bash
# Terminal 1 — API on :5000
cd backend/LibraMS.Api && dotnet run

# Terminal 2 — frontend on :5173
cd frontend && npm run dev
```

Confirm the API is up before touching the UI:

```bash
curl http://localhost:5000/health     # → {"status":"ok"}
```

### Production — pre-warm first

The backend runs on Azure App Service **F1**, which has no "Always On". After ~5
minutes idle the first request cold-starts for 5–10s. Hit health once and wait for it
to answer before starting, or step 1 will look broken when it is merely asleep:

```bash
curl https://<your-api>/health        # → {"status":"ok"}
```

> **Production writes are real.** Steps 3–6 create a loan and edit a book in the live
> database. Do them against a book you have added for the purpose — step 5 has you
> create one — and finish [Cleanup](#cleanup) so the catalog is left as you found it.

---

## Accounts

Sign-in is Google OAuth through Supabase; there are no seeded test accounts.

- **Librarian** — needed for steps 5 and 6. After a first sign-in, open Supabase
  Dashboard → Table Editor → `library_users`, set your row's `role` to `librarian`,
  then sign out and back in. The role is read from a claim, so the round trip is
  required.
- **Member** — a second Google account, left at the default `member` role. Only step
  4b needs it. If you have no second account, 4b is skippable; note it as untested
  rather than passing it.

---

## The pass

### 1. Sign in

1. Open the frontend. You should land on `/login`.
2. Sign in with Google as the **librarian** account.
3. You are redirected to the dashboard, which shows catalog statistics.

> **Pass:** the dashboard renders with your name and stats.
>
> **Watch for (BUG-8):** the dashboard should appear without a stall. Signing keys are
> now fetched once and cached for an hour, so only the very first authenticated
> request after an API restart pays for a JWKS round trip. A consistent per-request
> delay means the cache is not being hit.
>
> **Watch for (BUG-7):** on production, an origin refused by CORS fails here first.
> Open DevTools → Console: a CORS error rather than a `401` means the deployed
> `Frontend__Url` does not match the origin you loaded.

### 2. Search and filter

1. Go to **Books**.
2. Type a title fragment into the search box — matching books narrow down.
3. Open the **Genre** dropdown and pick **`Fiction`**.

> **Pass:** results are *only* books whose genre is exactly `Fiction`.
>
> **This is BUG-4.** Before the fix the filter wrapped the term in `%…%`, so `Fiction`
> also returned `Non-Fiction` and `Science Fiction`. If you see either in the results,
> the fix has regressed. If your catalog has no such genres, add a `Science Fiction`
> book in step 5 first — the check is meaningless without a substring neighbour.
>
> Then select **`Science Fiction`**: it must still return that genre. Exact matching
> stays case-insensitive, so the dropdown values always match.

### 3. Check out

1. Open any book showing **Available**.
2. Click **Check out**.

> **Pass:** the button resolves to a success state and the book flips to
> **Checked out**. Go to **My Loans** — the loan is listed with a due date.
>
> **This is BUG-1, the one that matters most.** Checkout inserts `loans.user_email`,
> a column `schema.sql` never created. Every checkout previously failed with an opaque
> `500 {"error":"An unexpected error occurred."}`. A `500` here means the database you
> are pointed at predates the fix — apply the migration from
> [design.md](openspec/changes/librams-bug-fixes/design.md#migration-note-for-existing-deployments):
>
> ```sql
> ALTER TABLE public.loans ADD COLUMN IF NOT EXISTS user_email TEXT;
> ```

**3b. Unknown book returns 404 (BUG-3).** Not reachable from the UI — the catalog only
links real books. Check it against the API, with a bearer token copied from DevTools →
Application → Local Storage → the Supabase session key:

```bash
curl -i -X POST "<api>/api/loans/checkout/00000000-0000-0000-0000-000000000000" \
     -H "Authorization: Bearer <token>"
# → 404 Not Found          (before the fix: 409 Conflict)
```

Repeat against the book you just checked out — that one *should* answer `409` with
`{"error":"Book is not available for checkout."}`. The two codes distinguishing
correctly is the whole of BUG-3.

### 4. Check in

**4a — your own loan.**

1. Go to **My Loans**.
2. Click **Return** on the loan from step 3.

> **Pass:** the loan moves out of the active list and the book reads **Available**
> again.

**4b — someone else's loan (BUG-2).** The security check, and the reason step 4 needs
two accounts. The caller must be a **member** who does not own the loan — a librarian
is *allowed* to check in anything, so testing this as the librarian proves nothing.

1. As the **librarian**, check out a book. Copy that loan's `id` from Admin → Loans (or
   from `public.loans`).
2. Sign out, and sign in as the **member**. Copy the member's bearer token from
   DevTools → Application → Local Storage → the Supabase session key.
3. Call check-in on the librarian's loan with the member's token. The UI offers no path
   to this — My Loans only lists your own — so it has to go through the API:

```bash
curl -i -X POST "<api>/api/loans/checkin/<librarians-loan-id>" \
     -H "Authorization: Bearer <members-token>"
# → 404 Not Found
```

> **Pass:** `404`, and the loan is **still active** — confirm in the owner's My Loans.
> Before the fix this returned `200` and genuinely returned the book.
>
> `404` rather than `403` is deliberate: a `403` would confirm that the loan ID exists,
> letting someone enumerate other users' loans.

**4c — librarian override.** Sign back in as the librarian, go to **Admin → Loans**,
and click **Check in** on any member's active loan.

> **Pass:** it succeeds. Librarians return books at the desk, so their unrestricted
> check-in is intended and must survive the BUG-2 fix.

### 5. Edit a book

As the **librarian**.

1. **Books** → **Add book**. Title and author are required; give it a genre of
   `Science Fiction`, a description, and a cover URL. Save.
2. Reopen it with **Edit**, change the **title**, and save.

> **Pass:** the new title shows in the list and on the detail page.
>
> Optionally click **✨ AI describe** while adding: it fills the description and
> suggests genres. It is rate-limited to 10 requests per minute — **per caller** now,
> not globally (BUG-6). Exhausting it must not affect a second account, and a `429`
> answers `{"error":"Too many requests. Please try again later."}`.

### 6. Clear a field

Still on the book from step 5 — this is the step most likely to regress silently.

1. **Edit** the book.
2. Select the entire **Description** and delete it, leaving the box empty.
3. Save.

> **Pass:** reopen the book — the description is **gone**, not restored.
>
> **This is BUG-5.** Every field used `COALESCE(@Field, field)`, which cannot tell an
> explicit clear from an omitted field, so a description could be set but never
> removed. Now `genre`, `description`, and `cover_url` treat an empty submission as an
> explicit clear.
>
> Repeat for **Genre** and **Cover URL** — same three fields, same behaviour.
>
> **The other half of the fix:** `title` and `author` stay on `COALESCE` and are *not*
> clearable — the form marks them required, so the UI will not let you empty them.
> That is correct, not a bug.

---

## Cleanup

Skip on a local throwaway database; do it on production.

1. Return any book still checked out (steps 3, 4c).
2. Delete the book added in step 5 — **Edit** → **Delete**.
3. Revert any `library_users.role` you changed only for this pass.

---

## Recording the result

| # | Flow | Bug | Result |
|---|------|-----|--------|
| 1 | Sign in | BUG-8, BUG-7 | |
| 2 | Search and genre filter | BUG-4 | |
| 3 | Check out | BUG-1 | |
| 3b | Unknown book → `404` | BUG-3 | |
| 4a | Return own loan | — | |
| 4b | Cannot return another's loan | BUG-2 | |
| 4c | Librarian override | BUG-2 | |
| 5 | Edit a book | — | |
| 6 | Clear an optional field | BUG-5 | |

A failure is worth more than a pass here: every bug in this list shipped past a green
automated suite. If a step fails, capture the DevTools Network entry (status and
response body) before retrying — the API converts unhandled exceptions into an opaque
`500`, so the browser rarely shows the real cause and the API log is where it lives.

BUG-9 has no manual step by nature: it *is* the automated suite's reporting. Confirm it
with `dotnet test` and no `TEST_DB_CONNECTION_STRING` set — the database tests must
report **skipped**, not passed.

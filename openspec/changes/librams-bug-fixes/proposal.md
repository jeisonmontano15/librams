# Proposal: LibraMS — Bug Fixes

## Why

A code audit of the shipped system found nine defects, two of them serious enough to
affect a first-time evaluator:

1. **Checkout is broken everywhere, including production.** `LoanRepository.CheckOutAsync`
   inserts into a `loans.user_email` column that `schema.sql` never creates. Every
   checkout fails with `42703: column "user_email" does not exist` — a core feature that
   has never worked.

   This was initially diagnosed as schema drift, on the assumption that the deployed
   database had the column added out-of-band and only clean provisions were affected.
   Verification against the live database on 2026-08-14 disproved that: the column was
   absent there too, and `public.loans` held **zero rows** — no checkout had ever
   succeeded. The column has since been added to the live database (see the migration
   note in `design.md`).

2. **Any member can return anyone else's book.** `POST /api/loans/checkin/{loanId}`
   never checks that the loan belongs to the caller, so any authenticated user who
   guesses or enumerates a loan ID can return that book and free it for checkout.

The remaining seven range from incorrect HTTP status codes to a rate limiter that does
not partition per client. None are cosmetic; each produces observably wrong behaviour.

## What Changes

- Add the missing `user_email` column to the canonical schema so checkout works on a
  clean provision, and make loan reads tolerate rows written before it existed
- Restrict check-in to the loan's owner, with librarians retaining the ability to check
  in on a member's behalf at the desk
- Return `404` rather than `409` when checking out a book that does not exist
- Match genres exactly instead of by substring, so `Fiction` no longer returns
  `Non-Fiction`
- Allow librarians to clear a book's optional fields (description, genre, cover) on edit
- Partition the AI rate limit per client, so one user cannot exhaust the quota for
  everyone — aligning the implementation with the existing `api-rate-limiting` spec
- Fix the inert CORS wildcard configuration
- Cache JWKS signing keys and stop constructing a fresh `HttpClient` per token
  validation
- Add regression tests covering each fixed defect

## Scope

### In scope

- The nine defects enumerated in `design.md`, each with a regression test
- Delta specs for the capabilities whose specified behaviour changes:
  `loan-management`, `book-search`, `book-management`, `api-rate-limiting`,
  `authentication-authorization`
- A schema migration note for existing deployments

### Out of scope

- The `COALESCE` partial-update mechanism is replaced only for the fields where
  clearing is meaningful; a general JSON Patch layer is not introduced
- No new product features; no UI redesign
- No change to the AI provider, hosting, or authentication provider
- Backfilling `user_email` on historical loan rows beyond making reads tolerate nulls

## Constraints

- No breaking changes to the public API surface: existing request and response shapes
  are preserved, and only status codes documented below as incorrect may change
- The schema fix must be safe to run against the live database, which already has the
  `user_email` column — it must not fail on re-run
- Fixes must not weaken the existing row-level security posture
- Every behavioural fix must be covered by a test that fails before the fix

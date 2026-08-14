# LibraMS - Library Management System

A full-stack library management system with AI-powered features, built with .NET 8, React, Supabase, and Groq AI. Deployed on free-tier Azure services.

---

## Architecture

LibraMS follows a **client-server architecture** with a clear separation between frontend and backend, connected through a RESTful API. Authentication is delegated to Supabase (Google OAuth + JWT), and AI capabilities are powered by Groq's free-tier LLM API.

```
┌─────────────────────┐       ┌─────────────────────┐       ┌──────────────────┐
│   React Frontend    │──────▶│   .NET 8 Minimal    │──────▶│    Supabase      │
│   (Static Web App)  │  JWT  │   API (App Service)  │  SQL  │  (PostgreSQL +   │
│                     │◀──────│                      │◀──────│   Auth + RLS)    │
└─────────────────────┘       └──────────┬───────────┘       └──────────────────┘
                                         │
                              ┌──────────▼───────────┐
                              │     Groq AI API      │
                              │  (Llama 3.3 70B)     │
                              └──────────────────────┘
```

### Backend

The API is built with **ASP.NET Core Minimal API** using **Carter** for modular endpoint routing. Each resource (books, loans, AI, users) lives in its own Carter module. Data access uses **Dapper** with raw SQL, which maps naturally to PostgreSQL full-text search, transactions, and custom functions.

**Key layers:**
- **Endpoints** -- Carter modules handling HTTP routing, validation (FluentValidation), and authorization
- **Repositories** -- Dapper-based data access (BookRepository, LoanRepository, UserRepository)
- **Services** -- AI integration (GroqAiService), external APIs (OpenLibraryService)
- **Middleware** -- RoleEnrichmentMiddleware injects user roles from the database into JWT claims after authentication

**Authentication flow:** Supabase issues JWTs after Google OAuth sign-in. The API validates these tokens using Supabase's JWKS endpoint with ECC P-256 key support. After validation, the RoleEnrichmentMiddleware looks up the user's role (`member` or `librarian`) and adds it as a claim for authorization policies.

### Frontend

A **React 18 SPA** built with TypeScript and Vite. Uses **TanStack React Query** for server state management, **React Router** for navigation, and **Tailwind CSS** for styling. The Supabase JS SDK handles authentication, and an Axios instance auto-injects JWT tokens into every API request.

### Database

**Supabase PostgreSQL** with:
- Full-text search indexes on book title, author, and description using `to_tsvector`
- Row-Level Security (RLS) policies enforcing access control at the database level
- Automatic user profile creation via database triggers on auth signup
- A `mark_overdue_loans()` function scheduled via `pg_cron` to flip loan statuses

---

## How Supabase Is Used

Supabase plays **two separate roles** in LibraMS, and deliberately not a third one that its SDK would normally provide.

| Role | Consumer | Mechanism |
|------|----------|-----------|
| **Identity provider** | Frontend | `supabase-js` with the anon key -- **auth only** |
| **Managed PostgreSQL** | Backend | Raw Npgsql/TCP on port 5432 via Dapper |
| ~~Data API (PostgREST)~~ | -- | **Not used.** No `.from()` calls exist in the frontend |

The frontend never reads or writes data through the Supabase SDK. It obtains a JWT and nothing else; every byte of application data flows through the .NET API. This keeps all business rules -- loan limits, availability transitions, role checks -- in one enforceable place.

### End-to-End Flow

```
   Browser                Supabase                 .NET API              Postgres
      │                      │                        │                     │
 ┌────┴─────────────────── SIGN-IN ────────────────────────────────────────────┐
      │  signInWithOAuth     │                        │                     │
      │─────────────────────▶│  ◀── Google OIDC ──▶   │                     │
      │                      │────── upsert auth.users ─────────────────────▶│
      │                      │                        │   trigger fires:    │
      │                      │                        │   handle_new_user() │
      │                      │                        │   creates profile   │
      │                      │                        │   role='member'     │
      │  ◀── ES256 JWT ──────│                        │                     │
 └─────────────────────────────────────────────────────────────────────────────┘
      │                      │                        │                     │
 ┌────┴─────────────────── API CALL ───────────────────────────────────────────┐
      │  Authorization: Bearer <JWT>                  │                     │
      │──────────────────────────────────────────────▶│                     │
      │                      │  ◀── fetch JWKS ───────│  validate signature │
      │                      │                        │                     │
      │                      │                        │─ SELECT role ──────▶│
      │                      │                        │  inject user_role   │
      │                      │                        │  claim, check policy│
      │                      │                        │                     │
      │                      │                        │─ Dapper query ─────▶│
      │  ◀───────────────────────── JSON response ────│                     │
 └─────────────────────────────────────────────────────────────────────────────┘
```

A PlantUML source of this diagram, with more detail, lives at [`docs/supabase-usage.puml`](docs/supabase-usage.puml).

### The Four Integration Points

**1. Auth client** -- [`frontend/src/lib/supabase.ts`](frontend/src/lib/supabase.ts) creates the client from `VITE_SUPABASE_URL` and `VITE_SUPABASE_ANON_KEY`. [`hooks/useAuth.tsx`](frontend/src/hooks/useAuth.tsx) wraps it in a React context exposing `signInWithGoogle`, `signOut`, and the current session, and subscribes to `onAuthStateChange`.

**2. Token attachment** -- [`frontend/src/lib/api.ts`](frontend/src/lib/api.ts) registers an Axios request interceptor that calls `getSession()` and sets `Authorization: Bearer <access_token>` on every outgoing request.

**3. Token validation** -- [`Program.cs`](backend/LibraMS.Api/Program.cs) configures `JwtBearer` with `ValidIssuer = {Supabase:Url}/auth/v1` and `ValidAudience = "authenticated"`. Keys come from Supabase's JWKS endpoint through a custom `IssuerSigningKeyResolver`.

> **Why the resolver is hand-written:** Supabase publishes its public keys with `key_ops: ["verify"]`, and `JsonWebKeySet.GetSigningKeys()` silently skips those keys -- yielding an empty key set and a blanket 401. The resolver therefore constructs `ECDsaSecurityKey` instances directly from the JWK `x`/`y` coordinates on the P-256 curve. OIDC auto-discovery is also disabled (`Authority = null`) so this resolver is actually consulted.

**4. Role resolution** -- Supabase JWTs carry no application role. [`RoleEnrichmentMiddleware`](backend/LibraMS.Api/Middleware/RoleEnrichmentMiddleware.cs) runs after authentication, reads `sub` from the validated principal, queries `library_users.role`, and injects a `user_role` claim that the `LibrarianOnly` policy consumes.

### How the Two User Tables Connect

Supabase owns `auth.users`; the application owns `public.library_users`. They are bound by a foreign key and kept in sync by a trigger:

```sql
id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE
```

On insert into `auth.users`, the `on_auth_user_created` trigger calls `handle_new_user()` (a `SECURITY DEFINER` function), which creates the matching profile with `role = 'member'`. Promotion to `librarian` is a manual update -- see [Assign Librarian Role](#4-assign-librarian-role).

### A Note on RLS

[`schema.sql`](backend/LibraMS.Api/Data/schema.sql) enables Row-Level Security on all three tables and defines a complete policy set. Two things are worth understanding about how those policies behave in practice:

- **The backend bypasses them.** It connects as the `postgres` superuser, which is exempt from RLS. Authorization for API traffic is enforced entirely by the middleware and policies described above. The RLS rules are a second line of defence, not the primary one.
- **They matter because the anon key is public.** `VITE_SUPABASE_ANON_KEY` ships to every browser. RLS is the only thing preventing a holder of that key from reading or writing the tables directly through PostgREST, bypassing the API completely.

> **Verify before deploying:** confirm RLS is actually enabled on the live project (Supabase Dashboard > **Authentication** > **Policies**, or run `SELECT relname, relrowsecurity FROM pg_class WHERE relname IN ('books','loans','library_users')`). A database provisioned before `schema.sql` was applied in full can end up with the tables present but RLS off, which leaves them openly readable and writable via the anon key.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 18, TypeScript 5.6, Vite 5, Tailwind CSS 3.4, React Router 6, TanStack Query 5 |
| Backend | .NET 8, ASP.NET Core Minimal API, Carter 8.2, Dapper 2.1, FluentValidation 11.9 |
| Database & Auth | Supabase (PostgreSQL, Google OAuth, JWT, RLS) |
| AI | Groq API (Llama 3.3 70B) via OpenAI-compatible SDK |
| API Docs | Scalar OpenAPI Explorer (served at `/docs`) |
| Hosting | Azure Static Web Apps (frontend), Azure App Service F1 (backend) |
| CI/CD | GitHub Actions -- separate workflows for frontend and backend |
| Testing | xunit + Microsoft.AspNetCore.Mvc.Testing (backend), Vitest + Testing Library (frontend) |
| Containerization | Docker (Alpine-based multi-stage build) |

---

## Features

### Book Catalogue
- Full CRUD operations for librarians (create, edit, delete books)
- Full-text PostgreSQL search across title, author, and description
- Filter by genre, availability status, or both
- Paginated results (20 books per page)
- Book covers fetched from Open Library by ISBN

### Loan Management
- Members can check out books with a 14-day loan period
- Check-in via My Loans page or the Admin Loans panel
- Automatic overdue detection via scheduled database function
- Reading history tracking for all previously borrowed books

### Authentication & Authorization
- Google SSO via Supabase Auth
- JWT-based API authentication with ECC P-256 key validation
- Two roles: **Member** (browse, borrow, view recommendations) and **Librarian** (full CRUD, manage all loans, overdue panel, AI describe)
- Row-Level Security at the database level

### AI Features (Groq -- free, no credit card)
- **Smart Search** -- natural language queries like "available sci-fi with space travel", parsed into structured filters by the LLM
- **Auto-Describe** -- generates book descriptions and genre suggestions from title/author/ISBN
- **Recommendations** -- personalized picks based on borrowing history
- Rate-limited to 10 requests per 60 seconds

### Dashboard
- Live statistics: total books, available, checked out, overdue, total loans
- AI-powered book recommendations
- Overdue tracking with visual indicators

---

## Project Structure

```
├── .claude/
│   ├── commands/opsx/              # Slash command definitions
│   │   ├── explore.md              # /opsx:explore -- think & investigate
│   │   ├── propose.md              # /opsx:propose -- create change artifacts
│   │   ├── apply.md                # /opsx:apply -- implement tasks
│   │   └── archive.md              # /opsx:archive -- finalize & archive
│   └── skills/                     # Skill implementations backing the commands
├── openspec/
│   ├── changes/                    # Active and archived OpenSpec changes
│   └── specs/                      # Main specification library
├── .github/workflows/
│   ├── deploy-backend.yml          # Test → Build Docker → Push GHCR → Deploy Azure App Service
│   └── deploy-frontend.yml         # Test → Build React → Deploy Azure Static Web Apps
├── backend/
│   ├── Dockerfile                  # Multi-stage Alpine build, exposes port 8080
│   ├── LibraMS.Api/
│   │   ├── Program.cs              # App bootstrap: auth, DI, CORS, rate limiting, middleware
│   │   ├── Endpoints/Endpoints.cs  # Carter modules: books, loans, AI, users, health
│   │   ├── Models/Models.cs        # Domain records, DTOs, enums
│   │   ├── Data/
│   │   │   ├── schema.sql          # Full DB schema, RLS policies, triggers, seed data
│   │   │   ├── DbConnectionFactory.cs
│   │   │   ├── BookRepository.cs   # Full-text search, CRUD, stats queries
│   │   │   ├── LoanRepository.cs   # Checkout/checkin with DB transactions
│   │   │   └── UserRepository.cs   # Profile lookup/creation
│   │   ├── Services/
│   │   │   ├── IAiService.cs       # AI service interface
│   │   │   ├── GroqAiService.cs    # Groq/Llama integration via OpenAI SDK
│   │   │   └── OpenLibraryService.cs
│   │   └── Middleware/
│   │       └── RoleEnrichmentMiddleware.cs
│   └── LibraMS.Api.Tests/          # xunit integration tests
│       ├── Fixtures/TestDbFixture.cs
│       ├── Endpoints/BooksEndpointTests.cs
│       └── Repositories/
│           ├── BookRepositoryTests.cs
│           └── LoanRepositoryTests.cs
└── frontend/
    ├── package.json
    ├── vite.config.ts
    ├── .env.example
    └── src/
        ├── App.tsx                 # Routes, QueryClient, auth guard
        ├── types/index.ts          # TypeScript interfaces mirroring backend models
        ├── lib/
        │   ├── supabase.ts         # Supabase client init
        │   └── api.ts              # Axios with auto-injected JWT
        ├── hooks/
        │   ├── useAuth.tsx         # Auth context: session, profile, role, sign-in/out
        │   └── useApi.ts           # TanStack Query hooks for all endpoints
        ├── pages/
        │   ├── LoginPage.tsx       # Google SSO sign-in
        │   ├── Dashboard.tsx       # Stats + AI recommendations
        │   ├── BooksPage.tsx       # Catalogue with dual search (standard + AI)
        │   ├── BookDetail.tsx      # Detail view, checkout, AI describe
        │   ├── MyLoans.tsx         # User's active loans + history
        │   └── AdminLoans.tsx      # Librarian: all loans + overdue panel
        └── components/
            ├── layout/Layout.tsx   # Sidebar navigation, user menu
            └── books/BookFormModal.tsx
```

---

## Running Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Supabase](https://supabase.com) account (free)
- [Groq API key](https://console.groq.com) (free, no credit card)

Optional -- only needed to run the spec tooling described in
[Specification-Driven Development with OpenSpec](#specification-driven-development-with-openspec).
The application builds, tests, and runs without it:

```bash
npm install -g @fission-ai/openspec@1.2.0
```

### 1. Database Setup

1. Create a new project at [supabase.com](https://supabase.com)
2. Go to **SQL Editor** and run the full contents of [`backend/LibraMS.Api/Data/schema.sql`](backend/LibraMS.Api/Data/schema.sql)
3. Enable **Google** under **Authentication > Providers** with your Google Cloud OAuth credentials
4. From **Project Settings > API**, copy: Project URL, `anon public` key, JWT Secret
5. From **Project Settings > Database**, copy the direct connection string
6. *(Optional)* Enable `pg_cron` extension and schedule overdue detection:
   ```sql
   SELECT cron.schedule('mark-overdue', '0 * * * *', $$SELECT mark_overdue_loans()$$);
   ```

### 2. Backend

```bash
cd backend/LibraMS.Api

dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Supabase" "Host=...;Port=5432;Database=postgres;Username=postgres;Password=...;SSL Mode=Require"
dotnet user-secrets set "Supabase:Url"       "https://YOUR_PROJECT.supabase.co"
dotnet user-secrets set "Supabase:JwtSecret" "YOUR_JWT_SECRET"
dotnet user-secrets set "Groq:ApiKey"        "gsk_..."
dotnet user-secrets set "Frontend:Url"       "http://localhost:5173"

dotnet run
# API  →  http://localhost:5000
# Docs →  http://localhost:5000/docs
```

### 3. Frontend

```bash
cd frontend
cp .env.example .env.local
```

Edit `.env.local`:
```
VITE_SUPABASE_URL=https://YOUR_PROJECT.supabase.co
VITE_SUPABASE_ANON_KEY=your-anon-key
VITE_API_URL=http://localhost:5000
```

```bash
npm install
npm run dev
# App → http://localhost:5173
```

### 4. Assign Librarian Role

After your first sign-in, go to the Supabase Dashboard > **Table Editor** > `library_users`, find your row, and change `role` from `member` to `librarian`. Sign out and back in.

---

## Running Tests

```bash
# Backend (requires TEST_DB_CONNECTION_STRING environment variable)
cd backend/LibraMS.Api.Tests
dotnet test

# Frontend
cd frontend
npm test
```

Without `TEST_DB_CONNECTION_STRING` the database-backed tests report as **skipped**, not
passed. A run you can trust reports `0 skipped`.

### Manual Smoke Test

The flows the automated suite cannot reach -- OAuth sign-in, checkout, check-in, and
clearing a book field -- are covered by [SMOKE-TEST.md](SMOKE-TEST.md), which runs against
either a local stack or production and names the defect each step guards.

---

## API Reference

Interactive API docs are available at `/docs` (Scalar UI) when the backend is running.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/books` | Public | Search books -- query params: `query`, `genre`, `status`, `page`, `pageSize` |
| `GET` | `/api/books/:id` | Public | Get single book by ID |
| `GET` | `/api/books/genres` | Public | List all genres |
| `GET` | `/api/books/stats` | Member | Dashboard statistics |
| `POST` | `/api/books` | Librarian | Create a book |
| `PUT` | `/api/books/:id` | Librarian | Update a book |
| `DELETE` | `/api/books/:id` | Librarian | Delete a book |
| `POST` | `/api/loans/checkout/:bookId` | Member | Check out a book |
| `POST` | `/api/loans/checkin/:loanId` | Member | Return a book |
| `GET` | `/api/loans/my` | Member | Current user's active loans |
| `GET` | `/api/loans/my/history` | Member | Current user's loan history |
| `GET` | `/api/loans/active` | Librarian | All active loans |
| `GET` | `/api/loans/overdue` | Librarian | All overdue loans |
| `POST` | `/api/ai/describe` | Librarian | AI-generate book description and genre suggestions |
| `POST` | `/api/ai/search` | Member | Natural language search |
| `GET` | `/api/ai/recommend` | Member | Personalized book recommendations |
| `GET` | `/api/users/me` | Member | Get or create user profile |
| `GET` | `/health` | Public | Health check endpoint |

---

## Deployment (Azure Free Tier)

Both the frontend and backend are deployed automatically via GitHub Actions on push to `master`.

### Backend -- Azure App Service F1

1. Create a Resource Group, App Service Plan (Free F1, Linux), and Web App (Container)
2. Set application settings:

   | Variable | Value |
   |----------|-------|
   | `ConnectionStrings__Supabase` | PostgreSQL connection string |
   | `Supabase__Url` | `https://xxx.supabase.co` |
   | `Supabase__JwtSecret` | JWT secret from Supabase |
   | `Groq__ApiKey` | `gsk_...` |
   | `Frontend__Url` | `https://your-frontend.azurestaticapps.net` |
   | `WEBSITES_PORT` | `8080` |

3. Download the Publish Profile and save as GitHub secret `AZURE_WEBAPP_PUBLISH_PROFILE`
4. Set GitHub secret `AZURE_WEBAPP_NAME`

> **Note:** Free tier has no "Always On". First request after ~5 min idle takes 5-10s to cold-start. Hit `GET /health` to pre-warm.

### Frontend -- Azure Static Web Apps

1. Create a Static Web App (Free plan) linked to your GitHub repo
2. Delete the auto-generated workflow (the repo has its own)
3. Save the Deployment Token as GitHub secret `AZURE_STATIC_WEB_APPS_API_TOKEN`
4. Set GitHub secrets: `VITE_SUPABASE_URL`, `VITE_SUPABASE_ANON_KEY`, `VITE_API_URL`

### Supabase -- Redirect URL

Add your frontend URL to **Authentication > URL Configuration > Redirect URLs** in Supabase.

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Groq over OpenAI/Anthropic** | Permanently free, no credit card. OpenAI SDK-compatible -- switching providers requires changing only `base_url` and `model` in GroqAiService. Llama 3.3 70B delivers 300+ tokens/sec with reliable JSON output. |
| **Dapper over Entity Framework** | Full-text search with `to_tsvector`, transactional checkout, and the overdue SQL function all map naturally to raw SQL. Dapper adds zero overhead for these queries. |
| **Carter for endpoints** | `ICarterModule` gives each resource its own clean class without controller boilerplate. |
| **Azure App Service F1 over Container Apps** | Truly free with no time expiry. Container Apps consumption billing can creep in unexpectedly. |
| **Azure Static Web Apps over Blob Storage** | SWA Free tier bundles CDN, HTTPS, custom domains, GitHub Actions integration, and SPA routing fallback at zero cost. |
| **Supabase for auth + database** | Combines PostgreSQL, Google OAuth, JWT issuance, and RLS in one free-tier service, reducing infrastructure complexity. |

---

## Specification-Driven Development with OpenSpec

This project was designed, specified, and implemented using **OpenSpec** -- a structured workflow framework for AI-assisted software development. OpenSpec enforces a four-phase lifecycle for every change, ensuring that design decisions are captured as artifacts before any code is written.

### Change Lifecycle

```
  Explore          Propose            Apply             Archive
 ┌─────────┐    ┌───────────┐    ┌───────────┐    ┌───────────────┐
 │ Think &  │───▶│ Create    │───▶│ Implement │───▶│ Finalize &    │
 │ Discover │    │ Artifacts │    │ Tasks     │    │ Sync Specs    │
 └─────────┘    └───────────┘    └───────────┘    └───────────────┘
                 proposal.md       Code changes     Move to archive/
                 design.md         Check off tasks   Sync delta specs
                 tasks.md
```

1. **Explore** -- investigate the problem space, map existing architecture, compare approaches. No code changes allowed -- this phase is purely for thinking.
2. **Propose** -- create a formal change with three core artifacts:
   - `proposal.md` -- what is being built and why, scope boundaries, constraints
   - `design.md` -- how it will be built, technical approach, integration points
   - `tasks.md` -- ordered implementation steps with markdown checkboxes
3. **Apply** -- work through tasks sequentially, making code changes and checking off completed items. If implementation reveals design issues, artifacts can be updated mid-flight.
4. **Archive** -- validate completion, sync any delta specs to the main spec library, and move the change to the timestamped archive.

### Change History

This project was built through the following OpenSpec changes:

| Change | Status | Description |
|--------|--------|-------------|
| `librams-initial-implementation` | Archived | Full system build: .NET 8 API, React frontend, Supabase integration, Groq AI features, Azure deployment, CI/CD |
| `librams-gap-fixes` | Archived | Post-implementation fixes: missing UserRepository, tsconfig, enum serialization bug, .gitignore, tests, rate limiting, health check, validators |

### Specification Library

Archiving those changes synced their delta specs into `openspec/specs/`, which now holds
**46 requirements across 11 capabilities** -- a living specification of the system as built:

| Capability | Requirements |
|------------|--------------|
| `book-management` | 8 |
| `authentication-authorization` | 7 |
| `book-search` | 6 |
| `loan-management` | 6 |
| `ai-features` | 5 |
| `backend-testing` | 4 |
| `frontend-testing` | 4 |
| `api-rate-limiting` | 2 |
| `book-cover-enrichment` | 2 |
| `user-repository` | 2 |
| `health-check` | 1 |

Every spec passes `openspec validate <capability> --type spec --strict`.

### Directory Layout

```
openspec/
├── changes/
│   ├── <change-name>/              # Active changes (none at present)
│   │   ├── .openspec.yaml          # Change metadata and schema
│   │   ├── proposal.md             # What & why
│   │   ├── design.md               # How
│   │   ├── tasks.md                # Implementation steps (checkbox tracking)
│   │   └── specs/                  # Delta specs (synced to main on archive)
│   └── archive/                    # Completed changes (YYYY-MM-DD-<name>/)
└── specs/                          # Main specification library (11 capabilities)
```

### Artifact Dependencies

Artifacts follow a dependency chain: the proposal must be written before the design (which references it), and the design must be written before tasks. The `openspec status` CLI command tracks which artifacts are complete and which are ready to be created.

### Delta Specs

Each change can include spec modifications under `openspec/changes/<name>/specs/`. These delta specs describe new or modified capabilities (e.g., `api-rate-limiting`, `backend-testing`, `health-check`). On archive, they are synced to the main `openspec/specs/` tree, building up a living specification of the entire system.

---

## Claude Code Integration

This project is configured for development with **Claude Code** (Anthropic's AI coding assistant). The `.claude/` directory contains custom slash commands and skills that integrate Claude Code with the OpenSpec workflow.

### Slash Commands

| Command | Description |
|---------|-------------|
| `/opsx:explore` | Enter explore mode -- a thinking partner for investigating problems, comparing approaches, and clarifying requirements. Claude reads the codebase and existing artifacts but does not write code. |
| `/opsx:propose` | Create a new change with all artifacts generated in sequence. Provide a description or change name, and Claude creates the proposal, design, and tasks artifacts following OpenSpec's dependency model. |
| `/opsx:apply` | Implement tasks from an active change. Claude reads all context artifacts (proposal, design, specs), then works through tasks sequentially -- making code changes and checking off items in `tasks.md`. |
| `/opsx:archive` | Finalize a completed change. Validates artifact and task completion, offers to sync delta specs to the main spec library, and moves the change to the timestamped archive directory. |

### How It Works

The commands are defined in `.claude/commands/opsx/` and backed by skill implementations in `.claude/skills/`. When invoked, Claude Code:

1. Uses the `openspec` CLI to query change status, artifact dependencies, and build instructions
2. Reads existing artifacts for context before creating new ones or implementing tasks
3. Follows the OpenSpec schema's rules for each artifact type (structure, constraints, dependencies)
4. Tracks progress via markdown checkboxes in `tasks.md`

### Project Instructions (CLAUDE.md)

The [CLAUDE.md](CLAUDE.md) file at the repository root provides Claude Code with project-level instructions: the OpenSpec workflow, CLI commands, directory layout, and artifact dependency model. This ensures Claude operates within the spec-driven workflow regardless of which slash command is used.

### Example Workflow

```bash
# 1. Explore an idea
/opsx:explore "should we add email notifications for overdue books?"

# 2. Propose the change (creates proposal.md, design.md, tasks.md)
/opsx:propose "add-overdue-notifications"

# 3. Implement all tasks from the change
/opsx:apply add-overdue-notifications

# 4. Archive when complete
/opsx:archive add-overdue-notifications
```

---

## License

MIT

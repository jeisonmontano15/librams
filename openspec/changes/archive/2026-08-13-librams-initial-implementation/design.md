# Design: LibraMS — Initial Implementation

## Architecture Overview

```
Browser
  │
  ├─► Azure Static Web Apps (React SPA)
  │       │ Axios + Bearer JWT
  │       ▼
  ├─► Azure App Service F1 (.NET 8 API)
  │       │ Dapper + Npgsql
  │       ▼
  └─► Supabase (PostgreSQL + Auth)
          │
          └─► RLS policies enforce access at DB level
```

All three layers run on permanently free hosting tiers. The backend is a stateless Docker container; the frontend is a static asset bundle.

---

## Tech Stack

| Layer | Technology | Version |
|---|---|---|
| Frontend | React + TypeScript + Vite + Tailwind CSS | React 18, TS 5.6, Vite 5.4 |
| Frontend hosting | Azure Static Web Apps (Free tier) | — |
| Backend | .NET 8 Minimal API (Carter + Dapper) | .NET 8 |
| Backend hosting | Azure App Service F1 (Linux container) | — |
| Database | PostgreSQL via Supabase | — |
| Auth | Supabase Auth (Google OAuth + JWT) | — |
| AI | Groq API — Llama 3.3 70B | OpenAI-compatible |
| CI/CD | GitHub Actions | — |

---

## Database Design

### Tables

#### `public.library_users`
| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | FK → `auth.users(id)` ON DELETE CASCADE |
| `email` | TEXT | NOT NULL, UNIQUE |
| `display_name` | TEXT | nullable |
| `role` | TEXT | DEFAULT `'member'`, CHECK IN (`'librarian'`, `'member'`) |
| `created_at` | TIMESTAMPTZ | DEFAULT NOW() |

Auto-populated via `on_auth_user_created` trigger → `handle_new_user()`.

#### `public.books`
| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | DEFAULT `uuid_generate_v4()` |
| `title` | TEXT | NOT NULL |
| `author` | TEXT | NOT NULL |
| `isbn` | TEXT | UNIQUE, nullable |
| `genre` | TEXT | nullable |
| `published_year` | INTEGER | nullable |
| `description` | TEXT | nullable |
| `cover_url` | TEXT | nullable |
| `status` | TEXT | DEFAULT `'available'`, CHECK IN (`'available'`, `'checked_out'`) |
| `created_at` | TIMESTAMPTZ | DEFAULT NOW() |
| `updated_at` | TIMESTAMPTZ | DEFAULT NOW(), auto-updated by trigger |

Indexes: GIN full-text (`books_fts_idx`) on title+author+description; `books_genre_idx`; `books_status_idx`.

#### `public.loans`
| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | DEFAULT `uuid_generate_v4()` |
| `book_id` | UUID | FK → `books(id)` ON DELETE CASCADE |
| `user_id` | UUID | FK → `library_users(id)` ON DELETE CASCADE |
| `checked_out_at` | TIMESTAMPTZ | DEFAULT NOW() |
| `due_date` | TIMESTAMPTZ | DEFAULT NOW() + 14 days |
| `returned_at` | TIMESTAMPTZ | nullable |
| `status` | TEXT | DEFAULT `'active'`, CHECK IN (`'active'`, `'returned'`, `'overdue'`) |
| `created_at` | TIMESTAMPTZ | DEFAULT NOW() |

Indexes: `loans_user_idx`, `loans_book_idx`, `loans_status_idx`.

### DB Functions

| Function | Purpose |
|---|---|
| `handle_new_user()` | Trigger: inserts into `library_users` on `auth.users` INSERT |
| `set_updated_at()` | Trigger: sets `books.updated_at = NOW()` before UPDATE |
| `mark_overdue_loans()` | Sets `loans.status = 'overdue'` where active and past due date. Called explicitly by the overdue endpoint. |

### Row Level Security

RLS enabled on all three tables.

| Table | Operation | Policy |
|---|---|---|
| `books` | SELECT | Any authenticated user |
| `books` | INSERT / UPDATE / DELETE | `role = 'librarian'` |
| `loans` | SELECT | Own loans OR `role = 'librarian'` |
| `loans` | INSERT | `user_id = auth.uid()` OR `role = 'librarian'` |
| `loans` | UPDATE | `role = 'librarian'` |
| `library_users` | SELECT | Own row OR `role = 'librarian'` |
| `library_users` | UPDATE | Own row only |

---

## Backend Design

### Project Structure

```
backend/
  Dockerfile
  LibraMS.Api/
    LibraMS.Api.csproj
    Program.cs                       # DI + Middleware pipeline
    appsettings.json
    Endpoints/Endpoints.cs           # BookEndpoints, LoanEndpoints, AiEndpoints, CreateBookValidator
    Models/Models.cs                 # Domain records, DTOs, AI DTOs
    Data/
      DbConnectionFactory.cs         # Npgsql connection from config
      BookRepository.cs              # IBookRepository + implementation + DashboardStats
      LoanRepository.cs              # ILoanRepository + implementation
      schema.sql                     # Full DB schema + RLS + seed data
    Services/
      GroqAiService.cs               # IAiService + GroqAiService (3 AI features)
      OpenLibraryService.cs          # IOpenLibraryService + implementation
    Middleware/
      RoleEnrichmentMiddleware.cs    # DB role → JWT claim injection
```

### Middleware Pipeline

Order in `Program.cs`:
1. `ExceptionHandler` — returns `{ "error": "..." }` with status 500
2. `UseCors` — allows frontend URL + `*.vercel.app`, credentials
3. `UseAuthentication` — validates Supabase JWT (HS256)
4. `UseAuthorization` — enforces policies
5. `RoleEnrichmentMiddleware` — queries DB for role, injects `user_role` claim
6. Carter routes + Scalar docs at `/docs`

### JWT Validation

```
Algorithm:    HS256
IssuerKey:    Supabase:JwtSecret
Issuer:       {Supabase:Url}/auth/v1
Audience:     "authenticated"
ValidateLifetime: true
```

### Authorization Policies

| Policy | Requirement |
|---|---|
| `AnyUser` | `RequireAuthenticatedUser()` |
| `LibrarianOnly` | `RequireClaim("user_role", "librarian")` |

### Domain Models

```csharp
// Core entities
record Book    { Guid Id; string Title; string Author; string? Isbn; string? Genre;
                 int? PublishedYear; string? Description; string? CoverUrl;
                 BookStatus Status; DateTime CreatedAt; DateTime UpdatedAt; }

record Loan    { Guid Id; Guid BookId; Guid UserId; string UserEmail;
                 DateTime CheckedOutAt; DateTime DueDate; DateTime? ReturnedAt;
                 LoanStatus Status; Book? Book; }

record LibraryUser { Guid Id; string Email; string? DisplayName; string Role; DateTime CreatedAt; }

// Stats
record DashboardStats(int TotalBooks, int Available, int CheckedOut, int Overdue, int TotalLoans);

// Enums
enum BookStatus { Available, CheckedOut }
enum LoanStatus { Active, Returned, Overdue }
```

### API Endpoints

#### Books (`/api/books`)

| Method | Path | Auth | Behaviour |
|---|---|---|---|
| GET | `/api/books` | Public | Full-text + genre + status filters; `PagedResult<Book>` |
| GET | `/api/books/genres` | Public | Distinct genre list |
| GET | `/api/books/stats` | AnyUser | `DashboardStats` |
| GET | `/api/books/{id}` | Public | Single book or 404 |
| POST | `/api/books` | LibrarianOnly | Create; FluentValidation on title/author/ISBN/year; 201 + Location |
| PUT | `/api/books/{id}` | LibrarianOnly | Partial update via COALESCE; 200 or 404 |
| DELETE | `/api/books/{id}` | LibrarianOnly | 204 or 404 |

#### Loans (`/api/loans`, base: AnyUser)

| Method | Path | Auth | Behaviour |
|---|---|---|---|
| POST | `/api/loans/checkout/{bookId}` | AnyUser | Transaction + `FOR UPDATE` lock; 201 or 409 conflict |
| POST | `/api/loans/checkin/{loanId}` | AnyUser | Transaction; 200 or 404 |
| GET | `/api/loans/my` | AnyUser | Active loans for caller + book JOIN |
| GET | `/api/loans/my/history` | AnyUser | All loans for caller (limit 20) + book JOIN |
| GET | `/api/loans/active` | LibrarianOnly | All active loans + book JOIN |
| GET | `/api/loans/overdue` | LibrarianOnly | Calls `mark_overdue_loans()` then returns overdue + book JOIN |

#### AI (`/api/ai`, base: AnyUser)

| Method | Path | Auth | Behaviour |
|---|---|---|---|
| POST | `/api/ai/describe` | LibrarianOnly | title+author+isbn → AI description + genre suggestions |
| POST | `/api/ai/search` | AnyUser | Natural query → parsed filters → `{ parsed, results }` |
| GET | `/api/ai/recommend` | AnyUser | Last 10 loans + 50 catalogue books → 3 recommendations |

### Checkout / Checkin Transaction Logic

**Checkout:**
1. Begin transaction
2. `SELECT status FROM books WHERE id = @bookId FOR UPDATE`
3. If status ≠ `'available'` → return null → 409
4. INSERT into `loans` RETURNING *
5. UPDATE `books.status = 'checked_out'`
6. Commit (rollback on error)

**Checkin:**
1. Begin transaction
2. `UPDATE loans SET status='returned', returned_at=NOW() WHERE id=@loanId AND status!='returned' RETURNING *`
3. If no row → return null → 404
4. UPDATE `books.status = 'available'`
5. Commit (rollback on error)

### AI Implementation (Groq)

All three features use the OpenAI .NET SDK pointed at Groq:

```
Model:    llama-3.3-70b-versatile
Endpoint: https://api.groq.com/openai/v1
Auth:     ApiKeyCredential(Groq:ApiKey)
```

Each feature sends a single user message with a structured prompt instructing the model to return strict JSON only. Parse failures are handled gracefully (log warning, return safe fallback).

| Feature | Input | Output |
|---|---|---|
| Describe | title, author, ISBN? | 2–3 sentence description + 1–2 genre tags |
| Search | natural language query + available genres | `{ query?, genre?, status?, explanation }` |
| Recommend | last 10 loans + 30 available books with UUIDs | 3 recommendations with `matchedBookId?` |

---

## Frontend Design

### Project Structure

```
frontend/src/
  types/index.ts           # All TS interfaces and enums
  lib/
    supabase.ts            # Supabase client singleton
    api.ts                 # Axios + JWT request interceptor
    utils.ts
  hooks/
    useAuth.tsx            # AuthProvider context + useAuth hook
    useApi.ts              # All TanStack Query hooks (17 total)
  components/
    layout/Layout.tsx      # Navigation shell
    books/BookFormModal.tsx # Create/Edit modal (librarians)
  pages/
    LoginPage.tsx
    Dashboard.tsx          # Stats + AI recommendations
    BooksPage.tsx          # Catalogue + dual search + filters + pagination
    BookDetail.tsx         # Book info + checkout/checkin + AI describe
    MyLoans.tsx            # Active loans + history tabs
    AdminLoans.tsx         # All active + overdue tabs (librarian)
  App.tsx                  # Router + QueryClient + AuthProvider + route guards
```

### Route Guards

```tsx
<Protected>               // Requires any authenticated session
<Protected librarianOnly> // Also requires isLibrarian = true
```

### Auth Flow

1. `signInWithGoogle()` → Supabase OAuth redirect
2. `onAuthStateChange` → stores `Session` in React state
3. `fetchProfile()` → `GET /api/users/me` → `LibraryUser` (role, displayName)
4. `isLibrarian = profile?.role === 'librarian'`
5. Axios interceptor injects `Authorization: Bearer <access_token>` on every request

### TanStack Query Hooks

All server state managed via TanStack Query v5. Mutations for books and loans invalidate `['books']`, `['loans']`, and `['stats']` on success.

| Hook | Endpoint | Cache |
|---|---|---|
| `useBooks` | GET /api/books | `['books', params]` |
| `useBook` | GET /api/books/:id | `['book', id]` |
| `useGenres` | GET /api/books/genres | `['genres']`, 5 min stale |
| `useStats` | GET /api/books/stats | `['stats']`, 30s refetch |
| `useCreateBook` | POST /api/books | mutation |
| `useUpdateBook` | PUT /api/books/:id | mutation |
| `useDeleteBook` | DELETE /api/books/:id | mutation |
| `useMyLoans` | GET /api/loans/my | `['loans','my']` |
| `useMyLoanHistory` | GET /api/loans/my/history | `['loans','my','history']` |
| `useAllActiveLoans` | GET /api/loans/active | `['loans','active']` |
| `useOverdueLoans` | GET /api/loans/overdue | `['loans','overdue']`, 60s refetch |
| `useCheckOut` | POST /api/loans/checkout/:id | mutation |
| `useCheckIn` | POST /api/loans/checkin/:id | mutation |
| `useAiDescribe` | POST /api/ai/describe | mutation |
| `useAiSearch` | POST /api/ai/search | mutation |
| `useAiRecommend` | GET /api/ai/recommend | `['ai','recommend']`, 10 min stale |

---

## CI/CD & Deployment

### Backend (`deploy-backend.yml`)

Trigger: push to `main` when `backend/**` changes.

1. Login to GitHub Container Registry (GHCR)
2. Build Docker image from `./backend/Dockerfile` (Alpine, multi-stage)
3. Push tags: `sha-<short>` + `latest`
4. Deploy to Azure App Service via `azure/webapps-deploy@v3`

Required secrets: `AZURE_WEBAPP_NAME`, `AZURE_WEBAPP_PUBLISH_PROFILE`.

### Frontend (`deploy-frontend.yml`)

Trigger: push to `main` when `frontend/**` changes.

Builds React (`tsc && vite build`) with injected env vars, deploys to Azure Static Web Apps.

Required secrets: `AZURE_STATIC_WEB_APPS_API_TOKEN`, `VITE_SUPABASE_URL`, `VITE_SUPABASE_ANON_KEY`, `VITE_API_URL`.

`frontend/public/staticwebapp.config.json` configures SPA fallback routing so all paths serve `index.html`.

---

## Environment Configuration

### Backend

| Key | Description |
|---|---|
| `ConnectionStrings:Supabase` | Npgsql connection string |
| `Supabase:Url` | Supabase project URL |
| `Supabase:JwtSecret` | JWT secret from Supabase settings |
| `Groq:ApiKey` | Groq API key (`gsk_...`) |
| `Frontend:Url` | Frontend URL for CORS |
| `WEBSITES_PORT` | `8080` — required for Azure App Service Linux |

### Frontend

| Variable | Description |
|---|---|
| `VITE_SUPABASE_URL` | Supabase project URL |
| `VITE_SUPABASE_ANON_KEY` | Supabase anon/public key |
| `VITE_API_URL` | Backend API base URL |

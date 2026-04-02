# Tasks: LibraMS — Initial Implementation

## Database

- [x] Create `library_users` table with role constraint and FK to `auth.users`
- [x] Create `books` table with all fields and status constraint
- [x] Create `loans` table with FK references to books and users and status constraint
- [x] Add GIN full-text index on books (title + author + description)
- [x] Add genre and status indexes on books
- [x] Add user, book, and status indexes on loans
- [x] Implement `handle_new_user()` trigger function — auto-create profile on sign-up
- [x] Implement `set_updated_at()` trigger function — auto-update `books.updated_at`
- [x] Implement `mark_overdue_loans()` function
- [x] Attach `on_auth_user_created` trigger to `auth.users`
- [x] Attach `books_updated_at` trigger to `books`
- [x] Enable RLS on `library_users`, `books`, and `loans`
- [x] Define RLS policies: books select/insert/update/delete
- [x] Define RLS policies: loans select/insert/update
- [x] Define RLS policies: library_users select/update
- [x] Insert seed data — 5 sample books with covers from Open Library

## Backend — Infrastructure

- [x] Create .NET 8 Minimal API project with Carter, Dapper, Npgsql, FluentValidation
- [x] Add all NuGet packages: OpenAI, JwtBearer, Serilog, Scalar, Azure.Identity
- [x] Configure JWT Bearer authentication (HS256, Supabase issuer and audience)
- [x] Define `LibrarianOnly` and `AnyUser` authorization policies
- [x] Register DI: `DbConnectionFactory`, repositories, AI service, HTTP client
- [x] Configure CORS (frontend URL + `*.vercel.app`, credentials)
- [x] Add global exception handler middleware returning JSON error
- [x] Implement `RoleEnrichmentMiddleware` — DB role lookup → `user_role` claim
- [x] Configure Serilog console logging
- [x] Expose Scalar interactive API docs at `/docs`
- [x] Implement `DbConnectionFactory` — Npgsql connection from config
- [x] Write Dockerfile — Alpine multi-stage build, port 8080
- [x] Configure `appsettings.json` with placeholder values

## Backend — Book Feature

- [x] Define `IBookRepository` interface
- [x] Implement `BookRepository.SearchAsync` — full-text + genre + status + pagination
- [x] Implement `BookRepository.GetByIdAsync`
- [x] Implement `BookRepository.CreateAsync`
- [x] Implement `BookRepository.UpdateAsync` — partial update via COALESCE
- [x] Implement `BookRepository.DeleteAsync`
- [x] Implement `BookRepository.SetStatusAsync`
- [x] Implement `BookRepository.GetGenresAsync`
- [x] Implement `BookRepository.GetStatsAsync` — single query for all 5 stats
- [x] Implement `BookEndpoints` Carter module (7 endpoints)
- [x] Implement `CreateBookValidator` — FluentValidation rules for title, author, ISBN, year

## Backend — Loan Feature

- [x] Define `ILoanRepository` interface
- [x] Implement `LoanRepository.CheckOutAsync` — transaction + `FOR UPDATE` lock
- [x] Implement `LoanRepository.CheckInAsync` — transaction
- [x] Implement `LoanRepository.GetActiveLoansByUserAsync` — JOIN with books
- [x] Implement `LoanRepository.GetAllActiveLoansAsync` — JOIN with books
- [x] Implement `LoanRepository.GetLoanHistoryByUserAsync` — JOIN with books, limit param
- [x] Implement `LoanRepository.GetOverdueLoansAsync` — calls `mark_overdue_loans()` first
- [x] Implement `LoanRepository.GetActiveLoanForBookAsync`
- [x] Implement `LoanEndpoints` Carter module (6 endpoints)

## Backend — AI & External Services

- [x] Define `IAiService` interface
- [x] Implement `GroqAiService` — OpenAI SDK pointed at Groq endpoint
- [x] Implement `DescribeBookAsync` — prompt → JSON parse → `AiDescribeResponse`
- [x] Implement `ParseNaturalSearchAsync` — prompt with genre list → structured filters
- [x] Implement `RecommendBooksAsync` — loan history + catalogue → 3 recommendations
- [x] Implement `AiEndpoints` Carter module (3 endpoints)
- [x] Implement `IOpenLibraryService` and `OpenLibraryService` — ISBN lookup via Open Library API

## Frontend — Foundation

- [x] Bootstrap Vite + React 18 + TypeScript project
- [x] Configure Tailwind CSS with PostCSS
- [x] Define all TypeScript types in `src/types/index.ts`
- [x] Create Supabase client singleton in `src/lib/supabase.ts`
- [x] Create Axios API client with JWT request interceptor in `src/lib/api.ts`
- [x] Implement `AuthProvider` and `useAuth` context hook
- [x] Implement `Protected` route guard with `librarianOnly` flag
- [x] Configure React Router v6 with all routes in `App.tsx`
- [x] Configure TanStack Query client (retry: 1, staleTime: 30s)
- [x] Add `react-hot-toast` toast provider

## Frontend — Data Hooks

- [x] Implement book query hooks: `useBooks`, `useBook`, `useGenres`, `useStats`
- [x] Implement book mutation hooks: `useCreateBook`, `useUpdateBook`, `useDeleteBook`
- [x] Implement loan query hooks: `useMyLoans`, `useMyLoanHistory`, `useAllActiveLoans`, `useOverdueLoans`
- [x] Implement loan mutation hooks: `useCheckOut`, `useCheckIn`
- [x] Implement AI hooks: `useAiDescribe`, `useAiSearch`, `useAiRecommend`
- [x] Wire cache invalidation on mutations (books, loans, stats)

## Frontend — Pages & Components

- [x] Implement `LoginPage` — Google sign-in button
- [x] Implement `Layout` — navigation shell with sidebar/header
- [x] Implement `Dashboard` — stats cards + AI recommendations
- [x] Implement `BooksPage` — catalogue with standard search, AI search toggle, genre filter, status filter, pagination
- [x] Implement `BookDetail` — book info, checkout/checkin button, AI describe for librarians
- [x] Implement `MyLoans` — active loans + loan history tabs
- [x] Implement `AdminLoans` — all active loans + overdue tab for librarians
- [x] Implement `BookFormModal` — create/edit book modal for librarians
- [x] Configure `staticwebapp.config.json` for Azure SWA SPA fallback routing

## CI/CD & Deployment

- [x] Write `deploy-backend.yml` — build Docker → push GHCR → deploy to Azure App Service
- [x] Write `deploy-frontend.yml` — build React → deploy to Azure Static Web Apps
- [x] Document all required GitHub secrets in README
- [x] Document Azure portal setup steps in README

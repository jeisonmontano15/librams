# LibraMS — Library Management System

A full-stack library management system built with **.NET 8 Minimal API**, **React + TypeScript**, **Supabase (PostgreSQL + Auth)**, and **Groq AI (free, no credit card)** — deployed entirely on **free Azure services**.

---

## Live Demo

| Service | URL |
|---------|-----|
| Frontend | `https://librams.azurestaticapps.net` _(replace after deploy)_ |
| API docs | `https://librams-api.azurewebsites.net/docs` |

**Demo accounts**

| Role | How to access |
|------|---------------|
| Member | Sign in with any Google account |
| Librarian | Sign in → Supabase Dashboard → Table Editor → `library_users` → set `role = 'librarian'` |

---

## Features

### Core

| Feature | Details |
|---------|---------|
| Book catalogue | Add, edit, delete books — title, author, ISBN, genre, year, description, cover |
| Check-out | Members borrow books; 14-day loan period tracked automatically |
| Check-in | Return books via My Loans or the Admin Loans panel |
| Search | Full-text PostgreSQL search across title, author, description |
| Filters | Filter by genre, availability status, or both |
| Pagination | 20 books per page |

### Authentication & Roles

| Feature | Details |
|---------|---------|
| Google SSO | One-click sign-in via Supabase Auth OAuth |
| JWT auth | Supabase-issued JWTs validated by the .NET API on every request |
| Role: Member | Browse catalogue, search, check out/in own books, view recommendations |
| Role: Librarian | Full CRUD on books, manage all loans, see overdue panel, use AI describe |
| Row-level security | PostgreSQL RLS policies enforce access at the database level |

### AI Features (Groq — permanently free, no credit card)

| Feature | How to use |
|---------|-----------|
| **Smart search** | Toggle "AI search" in the catalogue — type naturally, e.g. _"available sci-fi with space travel"_ |
| **Auto-describe** | Book detail page (librarian) → "AI describe" generates description + genre suggestions |
| **Recommendations** | Dashboard shows 3 personalised picks based on borrowing history |

### Bonus

- **Overdue tracking** — loans past due date highlighted red; librarians get a dedicated overdue tab
- **Dashboard stats** — live counts: total, available, checked-out, overdue
- **Reading history** — users can browse all previously borrowed books
- **Open Library integration** — covers fetched from openlibrary.org by ISBN
- **Scalar API docs** — interactive API explorer at `/docs`
- **GitHub Actions CI/CD** — automatic deploys to Azure on every push to `main`

---

## Tech Stack

| Layer | Technology | Cost |
|-------|-----------|------|
| Frontend | React 18 + TypeScript + Vite + Tailwind | Free |
| Frontend hosting | Azure Static Web Apps (Free tier) | **Free forever** |
| Backend | .NET 8 Minimal API + Carter + Dapper | Free |
| Backend hosting | Azure App Service F1 (Free tier) | **Free forever** |
| Database + Auth | Supabase Free tier (PostgreSQL + Google OAuth) | **Free forever** |
| AI | Groq API — Llama 3.3 70B (free, no credit card) | **Free** |
| CI/CD | GitHub Actions | Free |

---

## Project Structure

```
librams/
├── .github/
│   └── workflows/
│       ├── deploy-backend.yml      # Build Docker → push GHCR → Azure App Service
│       └── deploy-frontend.yml     # Build React → Azure Static Web Apps
├── README.md
├── backend/
│   ├── Dockerfile                  # Alpine-based, Azure App Service Linux compatible
│   └── LibraMS.Api/
│       ├── Program.cs              # Minimal API bootstrap + DI
│       ├── Endpoints/Endpoints.cs  # Carter modules: books, loans, AI
│       ├── Models/Models.cs        # Domain records + DTOs
│       ├── Data/
│       │   ├── schema.sql          # Full DB schema + RLS policies + seed data
│       │   ├── DbConnectionFactory.cs
│       │   ├── BookRepository.cs   # Full-text search, CRUD, stats
│       │   └── LoanRepository.cs   # Checkout/checkin with DB transactions
│       ├── Services/
│       │   ├── GroqAiService.cs    # All 3 AI features — OpenAI-compatible Groq SDK
│       │   └── OpenLibraryService.cs
│       └── Middleware/
│           └── RoleEnrichmentMiddleware.cs
└── frontend/
    └── src/
        ├── App.tsx
        ├── pages/
        │   ├── Dashboard.tsx       # Stats + AI recommendations
        │   ├── BooksPage.tsx       # Catalogue + dual search (standard + AI)
        │   ├── BookDetail.tsx      # Detail + checkout + AI describe
        │   ├── MyLoans.tsx
        │   ├── AdminLoans.tsx
        │   └── LoginPage.tsx
        ├── hooks/
        │   ├── useAuth.tsx
        │   └── useApi.ts           # TanStack Query hooks for every endpoint
        ├── components/
        │   ├── layout/Layout.tsx
        │   └── books/BookFormModal.tsx
        └── lib/
            ├── api.ts              # Axios with auto-injected Bearer token
            └── supabase.ts
```

---

## Local Development

### Prerequisites

- Node.js 20+
- .NET 8 SDK
- Free [Supabase](https://supabase.com) account
- Free [Groq](https://console.groq.com) API key — no credit card required

### 1 — Database setup

1. Create a Supabase project at [supabase.com](https://supabase.com)
2. **SQL Editor** → run the entire contents of `backend/LibraMS.Api/Data/schema.sql`
3. **Authentication → Providers** → enable **Google** → add OAuth credentials from Google Cloud Console
4. Copy from **Project Settings → API**:
   - Project URL
   - `anon public` key
   - JWT Secret (under JWT Settings)
5. Copy the direct connection string from **Project Settings → Database**

### 2 — Backend

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

### 3 — Frontend

```bash
cd frontend
cp .env.example .env.local
# Edit .env.local — fill VITE_SUPABASE_URL, VITE_SUPABASE_ANON_KEY, VITE_API_URL

npm install
npm run dev
# App → http://localhost:5173
```

### 4 — Make yourself a librarian

After first sign-in:
1. Supabase Dashboard → **Table Editor** → `library_users`
2. Find your row → change `role` from `member` to `librarian`
3. Sign out and back in

---

## Deployment to Azure (all free)

### Part A — Backend: Azure App Service F1

**One-time setup in the Azure Portal:**

1. Create a **Resource Group**: `librams-rg`
2. **App Service Plan** → Free F1 tier, Linux OS
3. **Web App** → Container → Linux
   - Name: `librams-api` → gives `librams-api.azurewebsites.net`
   - Publish: Container
4. **Configuration → Application Settings** — add all environment variables:

```
ConnectionStrings__Supabase  =  Host=db.xxx.supabase.co;...
Supabase__Url                =  https://xxx.supabase.co
Supabase__JwtSecret          =  your-jwt-secret
Groq__ApiKey                 =  gsk_...
Frontend__Url                =  https://librams.azurestaticapps.net
WEBSITES_PORT                =  8080
```

5. **Deployment Center** → download the **Publish Profile** → save contents as GitHub secret `AZURE_WEBAPP_PUBLISH_PROFILE`
6. Add GitHub secret `AZURE_WEBAPP_NAME` = `librams-api`

> **F1 cold-start note:** Free tier has no "Always On" feature. The first request after ~5 minutes of inactivity takes 5–10 seconds to warm up. This is normal for the free tier and expected behavior.

### Part B — Frontend: Azure Static Web Apps (Free)

1. Azure Portal → **Static Web Apps** → Create
   - Name: `librams`, Plan: **Free**
   - Source: GitHub → your repo → branch `main`
   - App location: `frontend`, Output location: `dist`
2. Azure auto-creates a workflow file — **delete it** from your repo (we use our own)
3. Copy the **Deployment Token** from the resource → save as GitHub secret `AZURE_STATIC_WEB_APPS_API_TOKEN`
4. Add frontend secrets to GitHub:

```
VITE_SUPABASE_URL       =  https://xxx.supabase.co
VITE_SUPABASE_ANON_KEY  =  your-anon-key
VITE_API_URL            =  https://librams-api.azurewebsites.net
```

### Part C — Supabase: add redirect URL

Supabase → **Authentication → URL Configuration** → Redirect URLs → add:
```
https://librams.azurestaticapps.net
```

### Trigger first deploy

```bash
git add .
git commit -m "initial deploy"
git push origin main
# Both GitHub Actions workflows fire automatically
```

Check progress in the **Actions** tab of your GitHub repository.

---

## API Reference

Full interactive docs at `/docs` (Scalar UI).

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/books` | Public | Search: `query`, `genre`, `status`, `page`, `pageSize` |
| GET | `/api/books/:id` | Public | Get single book |
| GET | `/api/books/genres` | Public | List all genres |
| GET | `/api/books/stats` | Member | Dashboard statistics |
| POST | `/api/books` | Librarian | Create book |
| PUT | `/api/books/:id` | Librarian | Update book |
| DELETE | `/api/books/:id` | Librarian | Delete book |
| POST | `/api/loans/checkout/:bookId` | Member | Check out a book |
| POST | `/api/loans/checkin/:loanId` | Member | Return a book |
| GET | `/api/loans/my` | Member | My active loans |
| GET | `/api/loans/my/history` | Member | My loan history |
| GET | `/api/loans/active` | Librarian | All active loans |
| GET | `/api/loans/overdue` | Librarian | All overdue loans |
| POST | `/api/ai/describe` | Librarian | AI-generate book description |
| POST | `/api/ai/search` | Member | Natural language search |
| GET | `/api/ai/recommend` | Member | Personalised recommendations |

---

## Design Decisions

**Why Groq instead of OpenAI/Anthropic?**
Groq is permanently free with no credit card. It's OpenAI SDK-compatible — switching to it is a single `base_url` and `model` change in `GroqAiService.cs`. Llama 3.3 70B on Groq delivers 300+ tokens/sec and handles structured JSON outputs reliably. If Groq rate limits become a bottleneck, swapping to Google AI Studio (Gemini Flash) or Mistral free tier requires changing only two lines.

**Why Azure App Service F1 over Azure Container Apps?**
F1 is truly free with no time expiry. Container Apps consumption billing can creep in unexpectedly. For a demo, F1's simplicity and zero cost wins. The cold-start is documented and expected.

**Why Azure Static Web Apps over Azure Storage static hosting?**
SWA Free tier bundles CDN, HTTPS, custom domains, GitHub Actions, and SPA routing fallback — all at zero cost. Blob static hosting needs a CDN profile for HTTPS, which adds cost.

**Why Dapper over Entity Framework?**
Full-text search with `to_tsvector`, the transactional checkout, and the overdue SQL function all map naturally to raw SQL. Dapper is precise and zero-overhead for these queries.

**Why Carter for endpoints?**
`ICarterModule` puts each resource (books, loans, AI) in its own clean class. No controller boilerplate, easy to navigate in a code review.

---

## Environment Variables Reference

### Backend (App Service → Configuration → Application Settings)

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__Supabase` | PostgreSQL direct connection string |
| `Supabase__Url` | Supabase project URL |
| `Supabase__JwtSecret` | JWT secret from Supabase project settings |
| `Groq__ApiKey` | Groq API key from console.groq.com |
| `Frontend__Url` | Frontend URL for CORS |
| `WEBSITES_PORT` | Set to `8080` — required on Azure App Service Linux |

### Frontend (GitHub repository secrets → injected at build time)

| Variable | Description |
|----------|-------------|
| `VITE_SUPABASE_URL` | Supabase project URL |
| `VITE_SUPABASE_ANON_KEY` | Supabase anon/public key |
| `VITE_API_URL` | Backend API base URL |

---

## License

MIT

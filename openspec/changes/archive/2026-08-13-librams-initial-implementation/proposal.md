# Proposal: LibraMS — Initial Implementation

## What

Build a full-stack library management system (LibraMS) consisting of:

- A **.NET 8 Minimal API** backend with Carter modules, Dapper, and PostgreSQL via Supabase
- A **React 18 + TypeScript** single-page application hosted on Azure Static Web Apps
- **Supabase** for PostgreSQL database, Row Level Security, and Google OAuth
- **Groq AI** (Llama 3.3 70B, free tier) for three AI-powered features
- **GitHub Actions** CI/CD deploying to Azure free-tier services

## Why

LibraMS is an academic assignment demonstrating a production-quality, full-stack system built entirely on free infrastructure. The goal is to showcase:

1. **Clean architecture** — Carter modules, repository pattern, DI, typed DTOs
2. **Security by default** — JWT validation, RLS at the DB level, role-based authorization middleware
3. **AI integration** — three distinct AI features via a cost-free API (Groq)
4. **Automated deployment** — zero-touch CI/CD from git push to live Azure services
5. **Zero running cost** — every service tier used is permanently free

## Scope

### In scope

- Book catalogue: CRUD (librarians), browse/search (members), full-text PostgreSQL search, genre/status filters, pagination
- Loan lifecycle: checkout (14-day period), check-in, overdue detection, transactional DB operations
- User roles: `member` (default) and `librarian` (manually elevated); role enforced at API and DB level
- AI features: auto-describe books, natural-language search, personalised recommendations
- Open Library integration: ISBN-based cover and metadata lookup
- Dashboard: live stats (total, available, checked-out, overdue, total loans)
- Deployment: Docker container on Azure App Service F1; React SPA on Azure Static Web Apps Free

### Out of scope

- Email notifications
- Reservations / waitlists
- Fine / payment tracking
- Admin user management UI (role changes done via Supabase Dashboard)
- Mobile native application

## Constraints

- All infrastructure must remain on permanently free tiers
- AI provider must not require a credit card (Groq free tier)
- Backend must be containerised for portability
- No secrets committed to the repository

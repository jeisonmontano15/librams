-- LibraMS — LOCAL TEST PROVISIONING ONLY. Not the canonical schema; see schema.sql.
--
-- schema.sql cannot run against a plain PostgreSQL because it references Supabase's
-- auth.users table and auth.uid(). This derives from it, dropping only those pieces:
--   * the auth.users FK on library_users.id and the sign-up trigger + function
--   * the FK on loans.user_id (tests check out books with GUIDs that have no user row)
--   * all RLS policies, which are expressed via auth.uid()
--   * the seed data, which tests create for themselves
--
-- Keep in sync with schema.sql when table definitions change. The SchemaRegressionTests
-- parse schema.sql directly, so they catch drift in the tables this file mirrors.
--
-- Usage:
--   createdb librams_test
--   psql -U postgres -d librams_test -f schema.local.sql
--   $env:TEST_DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=librams_test;Username=postgres;Password=postgres"

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE IF NOT EXISTS public.library_users (
    id          UUID PRIMARY KEY,
    email       TEXT NOT NULL UNIQUE,
    display_name TEXT,
    role        TEXT NOT NULL DEFAULT 'member' CHECK (role IN ('librarian', 'member')),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.books (
    id             UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    title          TEXT NOT NULL,
    author         TEXT NOT NULL,
    isbn           TEXT UNIQUE,
    genre          TEXT,
    published_year INTEGER,
    description    TEXT,
    cover_url      TEXT,
    status         TEXT NOT NULL DEFAULT 'available' CHECK (status IN ('available', 'checked_out')),
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS books_fts_idx ON public.books
    USING GIN (to_tsvector('english', title || ' ' || author || ' ' || COALESCE(description, '')));
CREATE INDEX IF NOT EXISTS books_genre_idx ON public.books (genre);
CREATE INDEX IF NOT EXISTS books_status_idx ON public.books (status);

CREATE TABLE IF NOT EXISTS public.loans (
    id             UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    book_id        UUID NOT NULL REFERENCES public.books(id) ON DELETE CASCADE,
    user_id        UUID NOT NULL,
    checked_out_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    due_date       TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '14 days'),
    returned_at    TIMESTAMPTZ,
    status         TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'returned', 'overdue')),
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE public.loans ADD COLUMN IF NOT EXISTS user_email TEXT;

CREATE INDEX IF NOT EXISTS loans_user_idx ON public.loans (user_id);
CREATE INDEX IF NOT EXISTS loans_book_idx ON public.loans (book_id);
CREATE INDEX IF NOT EXISTS loans_status_idx ON public.loans (status);

CREATE OR REPLACE FUNCTION public.mark_overdue_loans()
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE public.loans
    SET status = 'overdue'
    WHERE status = 'active' AND due_date < NOW();
END;
$$;

CREATE OR REPLACE FUNCTION public.set_updated_at()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN NEW.updated_at = NOW(); RETURN NEW; END;
$$;

DROP TRIGGER IF EXISTS books_updated_at ON public.books;
CREATE TRIGGER books_updated_at
    BEFORE UPDATE ON public.books
    FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

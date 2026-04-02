-- LibraMS Database Schema
-- Run this in Supabase SQL Editor

-- Enable UUID extension (already enabled in Supabase)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ── Users (mirror of auth.users with role) ───────────────────────────────────
CREATE TABLE IF NOT EXISTS public.library_users (
    id          UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    email       TEXT NOT NULL UNIQUE,
    display_name TEXT,
    role        TEXT NOT NULL DEFAULT 'member' CHECK (role IN ('librarian', 'member')),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Auto-create user profile on sign-up
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
    INSERT INTO public.library_users (id, email, display_name, role)
    VALUES (
        NEW.id,
        NEW.email,
        COALESCE(NEW.raw_user_meta_data->>'full_name', split_part(NEW.email, '@', 1)),
        'member'
    )
    ON CONFLICT (id) DO NOTHING;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- ── Books ─────────────────────────────────────────────────────────────────────
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

-- Full-text search index
CREATE INDEX IF NOT EXISTS books_fts_idx ON public.books
    USING GIN (to_tsvector('english', title || ' ' || author || ' ' || COALESCE(description, '')));

CREATE INDEX IF NOT EXISTS books_genre_idx ON public.books (genre);
CREATE INDEX IF NOT EXISTS books_status_idx ON public.books (status);

-- ── Loans ─────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.loans (
    id             UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    book_id        UUID NOT NULL REFERENCES public.books(id) ON DELETE CASCADE,
    user_id        UUID NOT NULL REFERENCES public.library_users(id) ON DELETE CASCADE,
    checked_out_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    due_date       TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '14 days'),
    returned_at    TIMESTAMPTZ,
    status         TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'returned', 'overdue')),
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS loans_user_idx ON public.loans (user_id);
CREATE INDEX IF NOT EXISTS loans_book_idx ON public.loans (book_id);
CREATE INDEX IF NOT EXISTS loans_status_idx ON public.loans (status);

-- Auto-update overdue status (run via pg_cron or check at query time)
CREATE OR REPLACE FUNCTION public.mark_overdue_loans()
RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    UPDATE public.loans
    SET status = 'overdue'
    WHERE status = 'active' AND due_date < NOW();
END;
$$;

-- Updated_at trigger for books
CREATE OR REPLACE FUNCTION public.set_updated_at()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN NEW.updated_at = NOW(); RETURN NEW; END;
$$;

DROP TRIGGER IF EXISTS books_updated_at ON public.books;
CREATE TRIGGER books_updated_at
    BEFORE UPDATE ON public.books
    FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();

-- ── Row Level Security ────────────────────────────────────────────────────────
ALTER TABLE public.library_users ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.books ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.loans ENABLE ROW LEVEL SECURITY;

-- Books: anyone authenticated can read; only librarians can write
CREATE POLICY "books_select" ON public.books FOR SELECT TO authenticated USING (true);
CREATE POLICY "books_insert" ON public.books FOR INSERT TO authenticated
    WITH CHECK ((SELECT role FROM public.library_users WHERE id = auth.uid()) = 'librarian');
CREATE POLICY "books_update" ON public.books FOR UPDATE TO authenticated
    USING ((SELECT role FROM public.library_users WHERE id = auth.uid()) = 'librarian');
CREATE POLICY "books_delete" ON public.books FOR DELETE TO authenticated
    USING ((SELECT role FROM public.library_users WHERE id = auth.uid()) = 'librarian');

-- Loans: users see own loans; librarians see all
CREATE POLICY "loans_select_own" ON public.loans FOR SELECT TO authenticated
    USING (user_id = auth.uid() OR
           (SELECT role FROM public.library_users WHERE id = auth.uid()) = 'librarian');
CREATE POLICY "loans_insert" ON public.loans FOR INSERT TO authenticated
    WITH CHECK (user_id = auth.uid() OR
                (SELECT role FROM public.library_users WHERE id = auth.uid()) = 'librarian');
CREATE POLICY "loans_update" ON public.loans FOR UPDATE TO authenticated
    USING ((SELECT role FROM public.library_users WHERE id = auth.uid()) = 'librarian');

-- Users: see own profile; librarians see all
CREATE POLICY "users_select_own" ON public.library_users FOR SELECT TO authenticated
    USING (id = auth.uid() OR
           (SELECT role FROM public.library_users WHERE id = auth.uid()) = 'librarian');
CREATE POLICY "users_update_own" ON public.library_users FOR UPDATE TO authenticated
    USING (id = auth.uid());

-- ── Seed data (sample books) ──────────────────────────────────────────────────
INSERT INTO public.books (title, author, isbn, genre, published_year, description, cover_url) VALUES
('The Pragmatic Programmer', 'David Thomas, Andrew Hunt', '9780135957059', 'Technology', 2019,
 'Your journey to mastery — from journeyman to master developer.', 'https://covers.openlibrary.org/b/isbn/9780135957059-M.jpg'),
('Dune', 'Frank Herbert', '9780441013593', 'Science Fiction', 1965,
 'A sweeping epic set in a distant future where noble houses control the desert planet Arrakis.', 'https://covers.openlibrary.org/b/isbn/9780441013593-M.jpg'),
('One Hundred Years of Solitude', 'Gabriel García Márquez', '9780060883287', 'Fiction', 1967,
 'The multi-generational story of the Buendía family in the fictional town of Macondo.', 'https://covers.openlibrary.org/b/isbn/9780060883287-M.jpg'),
('Clean Code', 'Robert C. Martin', '9780132350884', 'Technology', 2008,
 'A handbook of agile software craftsmanship.', 'https://covers.openlibrary.org/b/isbn/9780132350884-M.jpg'),
('Sapiens', 'Yuval Noah Harari', '9780062316097', 'History', 2011,
 'A brief history of humankind from the Stone Age to the twenty-first century.', 'https://covers.openlibrary.org/b/isbn/9780062316097-M.jpg')
ON CONFLICT DO NOTHING;

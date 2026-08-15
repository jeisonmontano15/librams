import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { vi } from 'vitest';
import { Layout } from '../components/layout/Layout';

vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({
    profile: { displayName: 'Ada Lovelace', email: 'ada@example.com', role: 'librarian' },
    isLibrarian: true,
    signOut: vi.fn(),
  }),
}));

vi.mock('../lib/supabase', () => ({
  supabase: {
    auth: {
      getSession: vi.fn().mockResolvedValue({ data: { session: null } }),
      onAuthStateChange: vi.fn().mockReturnValue({ data: { subscription: { unsubscribe: vi.fn() } } }),
    },
  },
}));

// The sidebar is a persistent column from `lg` up and an off-canvas drawer below it.
// jsdom applies no media queries, so these assert the drawer's open/closed state via the
// translate class the breakpoint variants override — not the rendered geometry.
const sidebar = () => screen.getByRole('complementary');

function renderLayout(initialPath = '/') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<p>Dashboard content</p>} />
          <Route path="books" element={<p>Books content</p>} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe('Layout responsive navigation', () => {
  it('keeps the drawer off-canvas until the menu button is pressed', () => {
    renderLayout();

    expect(sidebar().className).toContain('-translate-x-full');

    fireEvent.click(screen.getByRole('button', { name: /open navigation/i }));

    expect(sidebar().className).toContain('translate-x-0');
    expect(sidebar().className).not.toContain('-translate-x-full');
  });

  it('closes the drawer when a nav link is followed', () => {
    renderLayout();

    fireEvent.click(screen.getByRole('button', { name: /open navigation/i }));
    expect(sidebar().className).toContain('translate-x-0');

    fireEvent.click(screen.getByRole('link', { name: /books/i }));

    // Navigating with the drawer open would otherwise leave it covering the new page.
    expect(sidebar().className).toContain('-translate-x-full');
    expect(screen.getByText('Books content')).toBeInTheDocument();
  });

  it('closes the drawer on Escape', () => {
    renderLayout();

    fireEvent.click(screen.getByRole('button', { name: /open navigation/i }));
    fireEvent.keyDown(window, { key: 'Escape' });

    expect(sidebar().className).toContain('-translate-x-full');
  });

  it('retains the persistent-column classes so the drawer disappears at `lg`', () => {
    renderLayout();
    expect(sidebar().className).toContain('lg:translate-x-0');
    expect(sidebar().className).toContain('lg:static');
  });
});

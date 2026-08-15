import { useEffect, useState } from 'react';
import { Outlet, NavLink, useNavigate, useLocation } from 'react-router-dom';
import { BookOpen, LayoutDashboard, BookMarked, Users, LogOut, Sparkles, Menu, X } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { clsx } from 'clsx';
import toast from 'react-hot-toast';

export function Layout() {
  const { profile, isLibrarian, signOut } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  // The sidebar is a persistent column from `lg` up and an off-canvas drawer below it.
  const [navOpen, setNavOpen] = useState(false);

  // Tapping a nav link on mobile navigates *and* leaves the drawer covering the page it
  // just opened, so close it whenever the route changes.
  useEffect(() => setNavOpen(false), [location.pathname]);

  // The drawer sits above the page, so Escape needs to dismiss it the way the backdrop does.
  useEffect(() => {
    if (!navOpen) return;
    const onKeyDown = (e: KeyboardEvent) => { if (e.key === 'Escape') setNavOpen(false); };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [navOpen]);

  const handleSignOut = async () => {
    await signOut();
    toast.success('Signed out');
    navigate('/login');
  };

  const navItem = (to: string, icon: React.ReactNode, label: string) => (
    <NavLink
      to={to}
      end={to === '/'}
      className={({ isActive }) => clsx(
        'flex items-center gap-3 px-4 py-2.5 rounded-sm text-sm font-sans transition-colors',
        isActive
          ? 'bg-ink-900 text-paper font-medium'
          : 'text-ink-400 hover:text-ink-900 hover:bg-ink-100'
      )}
    >
      <span className="w-4 h-4">{icon}</span>
      {label}
    </NavLink>
  );

  return (
    <div className="flex h-[100dvh] bg-paper font-sans overflow-hidden">
      {/* Backdrop — mobile drawer only */}
      {navOpen && (
        <div
          onClick={() => setNavOpen(false)}
          className="fixed inset-0 z-30 bg-ink-900/50 lg:hidden"
          aria-hidden="true"
        />
      )}

      {/* Sidebar: drawer under `lg`, static column from `lg` up */}
      <aside
        className={clsx(
          'w-64 max-w-[85vw] flex-shrink-0 bg-paper-light border-r border-ink-100 flex flex-col',
          'fixed inset-y-0 left-0 z-40 transition-transform duration-200 ease-out',
          'lg:static lg:z-auto lg:w-60 lg:max-w-none lg:translate-x-0 lg:transition-none',
          navOpen ? 'translate-x-0' : '-translate-x-full'
        )}
      >
        {/* Logo */}
        <div className="px-6 pt-8 pb-6 border-b border-ink-100 flex items-start justify-between">
          <div>
            <div className="flex items-center gap-2.5">
              <BookOpen size={20} className="text-ochre" />
              <span className="font-serif text-xl text-ink-900 leading-none">LibraMS</span>
            </div>
            <p className="text-xs text-ink-300 mt-1 font-sans tracking-wide">Library Management</p>
          </div>
          <button
            onClick={() => setNavOpen(false)}
            aria-label="Close navigation"
            className="-mr-2 -mt-1 p-2 text-ink-400 hover:text-ink-900 lg:hidden"
          >
            <X size={18} />
          </button>
        </div>

        {/* Navigation */}
        <nav className="flex-1 overflow-y-auto px-3 py-4 space-y-0.5">
          {navItem('/', <LayoutDashboard size={16} />, 'Dashboard')}
          {navItem('/books', <BookOpen size={16} />, 'Books')}
          {navItem('/loans', <BookMarked size={16} />, 'My Loans')}
          {isLibrarian && navItem('/admin/loans', <Users size={16} />, 'Manage Loans')}
        </nav>

        {/* AI badge — hidden on short viewports, where nav and account matter more */}
        <div className="mx-3 mb-3 px-3 py-2.5 bg-ochre-light rounded-sm border border-ochre/20 hidden sm:block">
          <div className="flex items-center gap-2 text-ochre-dark">
            <Sparkles size={13} />
            <span className="text-xs font-medium">AI-powered</span>
          </div>
          <p className="text-xs text-ink-400 mt-1 leading-relaxed">Smart search, recommendations &amp; auto-descriptions</p>
        </div>

        {/* User / Sign out */}
        <div className="px-3 py-4 border-t border-ink-100">
          <div className="flex items-center gap-3 px-3 py-2">
            <div className="w-8 h-8 rounded-full bg-ink-900 flex items-center justify-center text-paper text-xs font-medium flex-shrink-0">
              {(profile?.displayName ?? profile?.email ?? 'U')[0].toUpperCase()}
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-xs font-medium text-ink-900 truncate">{profile?.displayName ?? profile?.email}</p>
              <p className="text-xs text-ink-300 capitalize">{profile?.role ?? 'member'}</p>
            </div>
          </div>
          <button
            onClick={handleSignOut}
            className="mt-1 w-full flex items-center gap-3 px-4 py-2 text-sm text-ink-400 hover:text-ink-900 hover:bg-ink-100 rounded-sm transition-colors"
          >
            <LogOut size={14} />
            Sign out
          </button>
        </div>
      </aside>

      {/* Main content */}
      <div className="flex flex-1 flex-col min-w-0">
        {/* Mobile top bar — the only way to reach the drawer under `lg` */}
        <header className="flex items-center gap-3 px-4 h-14 flex-shrink-0 border-b border-ink-100 bg-paper-light lg:hidden">
          <button
            onClick={() => setNavOpen(true)}
            aria-label="Open navigation"
            aria-expanded={navOpen}
            className="-ml-2 p-2 text-ink-500 hover:text-ink-900 rounded-sm hover:bg-ink-100 transition-colors"
          >
            <Menu size={20} />
          </button>
          <div className="flex items-center gap-2">
            <BookOpen size={18} className="text-ochre" />
            <span className="font-serif text-lg text-ink-900 leading-none">LibraMS</span>
          </div>
        </header>

        <main className="flex-1 overflow-y-auto">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

import { Outlet, NavLink, useNavigate } from 'react-router-dom';
import { BookOpen, LayoutDashboard, BookMarked, Users, LogOut, Sparkles } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { clsx } from 'clsx';
import toast from 'react-hot-toast';

export function Layout() {
  const { profile, isLibrarian, signOut } = useAuth();
  const navigate = useNavigate();

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
    <div className="flex h-screen bg-paper font-sans overflow-hidden">
      {/* Sidebar */}
      <aside className="w-60 flex-shrink-0 bg-paper-light border-r border-ink-100 flex flex-col">
        {/* Logo */}
        <div className="px-6 pt-8 pb-6 border-b border-ink-100">
          <div className="flex items-center gap-2.5">
            <BookOpen size={20} className="text-ochre" />
            <span className="font-serif text-xl text-ink-900 leading-none">LibraMS</span>
          </div>
          <p className="text-xs text-ink-300 mt-1 font-sans tracking-wide">Library Management</p>
        </div>

        {/* Navigation */}
        <nav className="flex-1 px-3 py-4 space-y-0.5">
          {navItem('/', <LayoutDashboard size={16} />, 'Dashboard')}
          {navItem('/books', <BookOpen size={16} />, 'Books')}
          {navItem('/loans', <BookMarked size={16} />, 'My Loans')}
          {isLibrarian && navItem('/admin/loans', <Users size={16} />, 'Manage Loans')}
        </nav>

        {/* AI badge */}
        <div className="mx-3 mb-3 px-3 py-2.5 bg-ochre-light rounded-sm border border-ochre/20">
          <div className="flex items-center gap-2 text-ochre-dark">
            <Sparkles size={13} />
            <span className="text-xs font-medium">AI-powered</span>
          </div>
          <p className="text-xs text-ink-400 mt-1 leading-relaxed">Smart search, recommendations & auto-descriptions</p>
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
      <main className="flex-1 overflow-y-auto">
        <Outlet />
      </main>
    </div>
  );
}

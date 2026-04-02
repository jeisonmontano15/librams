import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from 'react-hot-toast';
import { AuthProvider, useAuth } from './hooks/useAuth';
import { Layout } from './components/layout/Layout';
import { Dashboard }   from './pages/Dashboard';
import { BooksPage }   from './pages/BooksPage';
import { BookDetail }  from './pages/BookDetail';
import { MyLoans }     from './pages/MyLoans';
import { AdminLoans }  from './pages/AdminLoans';
import { LoginPage }   from './pages/LoginPage';
import { ReactNode } from 'react';

const qc = new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 1000 * 30 } } });

function Protected({ children, librarianOnly = false }: { children: ReactNode; librarianOnly?: boolean }) {
  const { session, isLibrarian, loading } = useAuth();
  if (loading) return <div className="flex h-screen items-center justify-center text-ink-400 font-sans">Loading…</div>;
  if (!session) return <Navigate to="/login" replace />;
  if (librarianOnly && !isLibrarian) return <Navigate to="/" replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <BrowserRouter>
          <Toaster position="top-right" toastOptions={{
            style: { fontFamily: 'DM Sans, sans-serif', fontSize: '14px' },
          }} />
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/" element={<Protected><Layout /></Protected>}>
              <Route index element={<Dashboard />} />
              <Route path="books" element={<BooksPage />} />
              <Route path="books/:id" element={<BookDetail />} />
              <Route path="loans" element={<MyLoans />} />
              <Route path="admin/loans" element={<Protected librarianOnly><AdminLoans /></Protected>} />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  );
}

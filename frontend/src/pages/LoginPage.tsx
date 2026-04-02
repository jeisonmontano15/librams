import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { BookOpen } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';

export function LoginPage() {
  const { session, signInWithGoogle, loading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (session) navigate('/', { replace: true });
  }, [session, navigate]);

  return (
    <div className="min-h-screen bg-ink-900 flex items-center justify-center p-6">
      {/* Decorative background text */}
      <div className="absolute inset-0 overflow-hidden opacity-5 select-none pointer-events-none">
        <p className="font-serif text-paper text-[12rem] leading-none whitespace-nowrap -rotate-12 translate-y-20 -translate-x-10">
          READ LEARN DISCOVER
        </p>
      </div>

      <div className="relative w-full max-w-sm">
        {/* Card */}
        <div className="bg-paper rounded-sm p-8 shadow-2xl">
          {/* Logo */}
          <div className="flex items-center gap-3 mb-8">
            <div className="w-10 h-10 bg-ink-900 rounded-sm flex items-center justify-center">
              <BookOpen size={20} className="text-ochre" />
            </div>
            <div>
              <h1 className="font-serif text-2xl text-ink-900 leading-none">LibraMS</h1>
              <p className="text-xs text-ink-300 font-sans mt-0.5">Library Management System</p>
            </div>
          </div>

          <h2 className="font-serif text-xl text-ink-900 mb-1">Welcome back</h2>
          <p className="text-sm text-ink-400 font-sans mb-8 leading-relaxed">
            Sign in with your Google account to access your library.
          </p>

          <button
            onClick={signInWithGoogle}
            disabled={loading}
            className="w-full flex items-center justify-center gap-3 bg-ink-900 text-paper font-sans text-sm font-medium py-3 px-4 rounded-sm hover:bg-ink-700 transition-colors disabled:opacity-60"
          >
            <svg width="18" height="18" viewBox="0 0 24 24">
              <path fill="#EA4335" d="M5.27 9.76A7.08 7.08 0 0 1 19.07 12h-7.1v3.42h4.06a3.65 3.65 0 0 1-1.57 2.33v1.93h2.54A7.62 7.62 0 0 0 19.07 12c0-.52-.05-1.03-.14-1.52"/>
              <path fill="#4285F4" d="M11.97 19.68c2.07 0 3.8-.69 5.07-1.85l-2.48-1.92a4.53 4.53 0 0 1-2.59.72 4.47 4.47 0 0 1-4.19-3.07H5.2v1.98a7.63 7.63 0 0 0 6.77 4.14"/>
              <path fill="#FBBC05" d="M7.78 13.56a4.37 4.37 0 0 1 0-2.84V8.74H5.2a7.62 7.62 0 0 0 0 6.8l2.58-1.98"/>
              <path fill="#34A853" d="M11.97 7.12a4.17 4.17 0 0 1 2.93 1.14l2.19-2.19A7.38 7.38 0 0 0 11.97 4a7.63 7.63 0 0 0-6.77 4.14l2.58 1.98a4.47 4.47 0 0 1 4.19-3"/>
            </svg>
            Continue with Google
          </button>

          <p className="text-center text-xs text-ink-300 mt-6 font-sans">
            Members get reading access · Librarians get full management tools
          </p>
        </div>
      </div>
    </div>
  );
}

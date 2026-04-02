import { BookOpen, BookMarked, AlertCircle, TrendingUp, Sparkles } from 'lucide-react';
import { useStats } from '../hooks/useApi';
import { useAuth } from '../hooks/useAuth';
import { useAiRecommend } from '../hooks/useApi';
import { useNavigate } from 'react-router-dom';
import { clsx } from 'clsx';

function StatCard({ label, value, icon, accent = false, warn = false }: {
  label: string; value: number | undefined; icon: React.ReactNode; accent?: boolean; warn?: boolean;
}) {
  return (
    <div className={clsx(
      'p-5 rounded-sm border',
      warn   ? 'bg-red-50 border-red-100' :
      accent ? 'bg-ochre-light border-ochre/20' :
               'bg-paper-light border-ink-100'
    )}>
      <div className={clsx(
        'w-8 h-8 rounded-sm flex items-center justify-center mb-3',
        warn   ? 'bg-red-100 text-red-600' :
        accent ? 'bg-ochre/20 text-ochre-dark' :
                 'bg-ink-100 text-ink-500'
      )}>
        {icon}
      </div>
      <p className={clsx('text-3xl font-serif', warn ? 'text-red-700' : accent ? 'text-ochre-dark' : 'text-ink-900')}>
        {value ?? '—'}
      </p>
      <p className="text-xs text-ink-400 font-sans mt-1">{label}</p>
    </div>
  );
}

export function Dashboard() {
  const { profile, isLibrarian } = useAuth();
  const { data: stats, isLoading: statsLoading } = useStats();
  const { data: recs, isLoading: recsLoading } = useAiRecommend();
  const navigate = useNavigate();

  return (
    <div className="p-8 max-w-5xl">
      {/* Header */}
      <div className="mb-8">
        <h1 className="font-serif text-3xl text-ink-900">
          Good {getTimeOfDay()}, {profile?.displayName?.split(' ')[0] ?? 'Reader'}
        </h1>
        <p className="text-ink-400 font-sans text-sm mt-1">
          {new Date().toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
        </p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-10">
        <StatCard label="Total books"  value={stats?.totalBooks}  icon={<BookOpen size={16} />} />
        <StatCard label="Available"    value={stats?.available}   icon={<BookOpen size={16} />} accent />
        <StatCard label="Checked out"  value={stats?.checkedOut}  icon={<BookMarked size={16} />} />
        <StatCard label="Overdue"      value={stats?.overdue}     icon={<AlertCircle size={16} />} warn={!!stats?.overdue} />
      </div>

      {/* AI Recommendations */}
      <div className="mb-8">
        <div className="flex items-center gap-2 mb-4">
          <Sparkles size={15} className="text-ochre" />
          <h2 className="font-serif text-lg text-ink-900">Recommended for you</h2>
          <span className="text-xs bg-ochre/10 text-ochre-dark border border-ochre/20 px-2 py-0.5 rounded-full font-sans">AI</span>
        </div>

        {recsLoading ? (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {[0,1,2].map(i => <div key={i} className="h-32 bg-ink-100 rounded-sm animate-pulse" />)}
          </div>
        ) : recs?.recommendations?.length ? (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {recs.recommendations.map((rec, i) => (
              <div
                key={i}
                onClick={() => rec.matchedBookId && navigate(`/books/${rec.matchedBookId}`)}
                className={clsx(
                  'p-4 bg-paper-light border border-ink-100 rounded-sm',
                  rec.matchedBookId && 'cursor-pointer hover:border-ochre/40 hover:bg-ochre-light transition-colors'
                )}
              >
                <p className="font-serif text-ink-900 leading-snug">{rec.title}</p>
                <p className="text-xs text-ink-400 font-sans mt-0.5">{rec.author}</p>
                <p className="text-xs text-ink-500 font-sans mt-2 leading-relaxed italic">"{rec.reason}"</p>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-ink-400 font-sans italic">Borrow a few books to get personalised recommendations.</p>
        )}
      </div>

      {/* Quick actions */}
      <div>
        <h2 className="font-serif text-lg text-ink-900 mb-4">Quick actions</h2>
        <div className="flex flex-wrap gap-3">
          <button
            onClick={() => navigate('/books')}
            className="px-4 py-2 bg-ink-900 text-paper text-sm font-sans rounded-sm hover:bg-ink-700 transition-colors"
          >
            Browse catalogue
          </button>
          <button
            onClick={() => navigate('/loans')}
            className="px-4 py-2 border border-ink-200 text-ink-700 text-sm font-sans rounded-sm hover:bg-ink-100 transition-colors"
          >
            My loans
          </button>
          {isLibrarian && (
            <button
              onClick={() => navigate('/admin/loans')}
              className="px-4 py-2 border border-ink-200 text-ink-700 text-sm font-sans rounded-sm hover:bg-ink-100 transition-colors"
            >
              Manage loans
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

function getTimeOfDay() {
  const h = new Date().getHours();
  return h < 12 ? 'morning' : h < 18 ? 'afternoon' : 'evening';
}

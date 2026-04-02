import { useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, Sparkles, Plus, BookOpen, Filter } from 'lucide-react';
import { clsx } from 'clsx';
import { debounce } from '../lib/utils';
import { useBooks, useGenres, useAiSearch, useCreateBook } from '../hooks/useApi';
import { useAuth } from '../hooks/useAuth';
import type { Book, BookStatus, CreateBookForm } from '../types';
import { BookFormModal } from '../components/books/BookFormModal';
import toast from 'react-hot-toast';

export function BooksPage() {
  const { isLibrarian } = useAuth();
  const navigate = useNavigate();
  const { data: genres } = useGenres();

  // Regular search state
  const [query,  setQuery]  = useState('');
  const [genre,  setGenre]  = useState('');
  const [status, setStatus] = useState<BookStatus | ''>('');
  const [page,   setPage]   = useState(1);

  // AI search state
  const [aiMode,   setAiMode]   = useState(false);
  const [aiQuery,  setAiQuery]  = useState('');
  const [aiResult, setAiResult] = useState<{ explanation: string } | null>(null);
  const [aiBooks,  setAiBooks]  = useState<Book[] | null>(null);

  const [showForm, setShowForm] = useState(false);

  const { data, isLoading } = useBooks(
    aiBooks ? { page: 1, pageSize: 0 } : { query, genre, status: status || undefined, page, pageSize: 20 }
  );

  const { mutateAsync: aiSearch, isPending: aiSearching } = useAiSearch();
  const { mutateAsync: createBook, isPending: creating }  = useCreateBook();

  const displayBooks = aiBooks ?? data?.items ?? [];

  const debouncedSetQuery = useCallback(debounce((v: unknown) => { setQuery(v as string); setPage(1); }, 400), []);

  const handleAiSearch = async () => {
    if (!aiQuery.trim()) return;
    const result = await aiSearch(aiQuery);
    setAiResult(result.parsed);
    setAiBooks(result.results.items);
    toast.success('AI search complete');
  };

  const clearAiSearch = () => { setAiBooks(null); setAiResult(null); setAiQuery(''); };

  const handleCreate = async (form: CreateBookForm) => {
    await createBook(form);
    toast.success('Book added to catalogue');
    setShowForm(false);
  };

  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="font-serif text-3xl text-ink-900">Catalogue</h1>
          <p className="text-ink-400 text-sm font-sans mt-1">
            {data?.total ?? 0} books in the library
          </p>
        </div>
        {isLibrarian && (
          <button
            onClick={() => setShowForm(true)}
            className="flex items-center gap-2 px-4 py-2 bg-ink-900 text-paper text-sm font-sans rounded-sm hover:bg-ink-700 transition-colors"
          >
            <Plus size={14} />
            Add book
          </button>
        )}
      </div>

      {/* Search mode toggle */}
      <div className="flex items-center gap-1 mb-4 bg-ink-100 rounded-sm p-1 w-fit">
        <button
          onClick={() => { setAiMode(false); clearAiSearch(); }}
          className={clsx('px-3 py-1.5 text-xs font-sans rounded-sm transition-colors',
            !aiMode ? 'bg-paper text-ink-900 shadow-sm' : 'text-ink-400 hover:text-ink-700')}
        >
          Standard search
        </button>
        <button
          onClick={() => setAiMode(true)}
          className={clsx('flex items-center gap-1.5 px-3 py-1.5 text-xs font-sans rounded-sm transition-colors',
            aiMode ? 'bg-paper text-ink-900 shadow-sm' : 'text-ink-400 hover:text-ink-700')}
        >
          <Sparkles size={11} />
          AI search
        </button>
      </div>

      {/* Search bar */}
      {aiMode ? (
        <div className="flex gap-2 mb-4">
          <div className="relative flex-1">
            <Sparkles size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-ochre" />
            <input
              value={aiQuery}
              onChange={e => setAiQuery(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && handleAiSearch()}
              placeholder='Try "available sci-fi books" or "historical fiction by women"'
              className="w-full pl-9 pr-4 py-2.5 bg-paper-light border border-ink-200 rounded-sm text-sm font-sans focus:outline-none focus:border-ochre/50 focus:ring-1 focus:ring-ochre/20"
            />
          </div>
          <button
            onClick={handleAiSearch}
            disabled={aiSearching || !aiQuery.trim()}
            className="px-4 py-2 bg-ochre text-paper text-sm font-sans rounded-sm hover:bg-ochre-dark transition-colors disabled:opacity-60"
          >
            {aiSearching ? 'Thinking…' : 'Search'}
          </button>
          {aiBooks && (
            <button onClick={clearAiSearch} className="px-3 py-2 border border-ink-200 text-ink-500 text-sm font-sans rounded-sm hover:bg-ink-100">
              Clear
            </button>
          )}
        </div>
      ) : (
        <div className="flex gap-2 mb-4">
          <div className="relative flex-1">
            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-ink-300" />
            <input
              onChange={e => debouncedSetQuery(e.target.value)}
              placeholder="Search by title, author, or description…"
              className="w-full pl-9 pr-4 py-2.5 bg-paper-light border border-ink-200 rounded-sm text-sm font-sans focus:outline-none focus:border-ink-400"
            />
          </div>
        </div>
      )}

      {/* AI explanation */}
      {aiResult && (
        <div className="mb-4 px-4 py-2.5 bg-ochre-light border border-ochre/20 rounded-sm text-sm font-sans text-ochre-dark flex items-start gap-2">
          <Sparkles size={13} className="mt-0.5 flex-shrink-0" />
          {aiResult.explanation}
        </div>
      )}

      {/* Filters (standard mode) */}
      {!aiMode && (
        <div className="flex flex-wrap gap-2 mb-6">
          <div className="flex items-center gap-1.5 text-xs text-ink-400">
            <Filter size={12} />
          </div>
          <button
            onClick={() => setStatus('')}
            className={clsx('px-3 py-1 text-xs font-sans rounded-full border transition-colors',
              status === '' ? 'bg-ink-900 text-paper border-ink-900' : 'border-ink-200 text-ink-500 hover:border-ink-400')}
          >
            All
          </button>
          <button
            onClick={() => setStatus('available')}
            className={clsx('px-3 py-1 text-xs font-sans rounded-full border transition-colors',
              status === 'available' ? 'bg-sage text-paper border-sage' : 'border-ink-200 text-ink-500 hover:border-ink-400')}
          >
            Available
          </button>
          <button
            onClick={() => setStatus('checked_out')}
            className={clsx('px-3 py-1 text-xs font-sans rounded-full border transition-colors',
              status === 'checked_out' ? 'bg-ink-500 text-paper border-ink-500' : 'border-ink-200 text-ink-500 hover:border-ink-400')}
          >
            Checked out
          </button>
          {genres?.map(g => (
            <button
              key={g}
              onClick={() => setGenre(genre === g ? '' : g)}
              className={clsx('px-3 py-1 text-xs font-sans rounded-full border transition-colors',
                genre === g ? 'bg-slate text-paper border-slate' : 'border-ink-200 text-ink-500 hover:border-ink-400')}
            >
              {g}
            </button>
          ))}
        </div>
      )}

      {/* Book grid */}
      {isLoading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 xl:grid-cols-5 gap-4">
          {Array.from({ length: 10 }).map((_, i) => (
            <div key={i} className="animate-pulse">
              <div className="aspect-[2/3] bg-ink-100 rounded-sm mb-2" />
              <div className="h-4 bg-ink-100 rounded mb-1 w-3/4" />
              <div className="h-3 bg-ink-100 rounded w-1/2" />
            </div>
          ))}
        </div>
      ) : displayBooks.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <BookOpen size={40} className="text-ink-200 mb-4" />
          <p className="font-serif text-xl text-ink-400">No books found</p>
          <p className="text-sm text-ink-300 font-sans mt-1">Try adjusting your search or filters</p>
        </div>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 xl:grid-cols-5 gap-x-4 gap-y-6">
          {displayBooks.map(book => (
            <BookCard key={book.id} book={book} onClick={() => navigate(`/books/${book.id}`)} />
          ))}
        </div>
      )}

      {/* Pagination */}
      {!aiBooks && data && data.total > 20 && (
        <div className="flex items-center justify-center gap-4 mt-10">
          <button
            disabled={page === 1}
            onClick={() => setPage(p => p - 1)}
            className="px-4 py-2 text-sm font-sans border border-ink-200 rounded-sm disabled:opacity-40 hover:bg-ink-100 transition-colors"
          >
            Previous
          </button>
          <span className="text-sm text-ink-400 font-sans">
            Page {page} of {Math.ceil(data.total / 20)}
          </span>
          <button
            disabled={page * 20 >= data.total}
            onClick={() => setPage(p => p + 1)}
            className="px-4 py-2 text-sm font-sans border border-ink-200 rounded-sm disabled:opacity-40 hover:bg-ink-100 transition-colors"
          >
            Next
          </button>
        </div>
      )}

      {/* Add book modal */}
      {showForm && (
        <BookFormModal
          onClose={() => setShowForm(false)}
          onSubmit={handleCreate}
          loading={creating}
        />
      )}
    </div>
  );
}

function BookCard({ book, onClick }: { book: Book; onClick: () => void }) {
  return (
    <div onClick={onClick} className="cursor-pointer group">
      {/* Cover */}
      <div className="aspect-[2/3] bg-ink-100 rounded-book shadow-book overflow-hidden mb-3 relative">
        {book.coverUrl ? (
          <img src={book.coverUrl} alt={book.title} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300" />
        ) : (
          <div className="w-full h-full flex flex-col items-center justify-center p-3 bg-gradient-to-br from-ink-800 to-ink-900">
            <p className="font-serif text-paper text-xs text-center leading-relaxed line-clamp-4">{book.title}</p>
          </div>
        )}
        {/* Status badge */}
        <div className={clsx(
          'absolute bottom-2 left-2 px-1.5 py-0.5 text-xs font-sans rounded',
          book.status === 'available' ? 'bg-sage text-white' : 'bg-ink-700 text-ink-200'
        )}>
          {book.status === 'available' ? 'In' : 'Out'}
        </div>
      </div>
      <p className="font-serif text-sm text-ink-900 line-clamp-2 leading-snug group-hover:text-ochre transition-colors">{book.title}</p>
      <p className="text-xs text-ink-400 font-sans mt-0.5 line-clamp-1">{book.author}</p>
    </div>
  );
}

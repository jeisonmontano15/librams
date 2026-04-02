import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, Sparkles, BookMarked, RotateCcw, Pencil, Trash2 } from 'lucide-react';
import { clsx } from 'clsx';
import { useBook, useCheckOut, useDeleteBook, useAiDescribe, useUpdateBook, useMyLoans } from '../hooks/useApi';
import { useAuth } from '../hooks/useAuth';
import { BookFormModal } from '../components/books/BookFormModal';
import toast from 'react-hot-toast';
import type { CreateBookForm } from '../types';

export function BookDetail() {
  const { id }      = useParams<{ id: string }>();
  const navigate    = useNavigate();
  const { isLibrarian } = useAuth();

  const { data: book, isLoading } = useBook(id);
  const { data: myLoans }         = useMyLoans();
  const { mutateAsync: checkOut, isPending: checkingOut } = useCheckOut();
  const { mutateAsync: deleteBook, isPending: deleting }  = useDeleteBook();
  const { mutateAsync: aiDescribe, isPending: aiLoading } = useAiDescribe();
  const { mutateAsync: updateBook, isPending: updating }  = useUpdateBook();

  const [showEdit, setShowEdit] = useState(false);
  const [aiDesc,   setAiDesc]   = useState<{ description: string; suggestedGenres: string[] } | null>(null);

  const activeLoan = myLoans?.find(l => l.bookId === id && l.status !== 'returned');

  const handleCheckOut = async () => {
    if (!id) return;
    await checkOut(id);
    toast.success('Book checked out! Due in 14 days.');
  };

  const handleDelete = async () => {
    if (!id || !confirm('Delete this book from the catalogue?')) return;
    await deleteBook(id);
    toast.success('Book deleted');
    navigate('/books');
  };

  const handleAiDescribe = async () => {
    if (!book) return;
    const result = await aiDescribe({ title: book.title, author: book.author, isbn: book.isbn });
    setAiDesc(result);
    toast.success('AI description generated');
  };

  const handleUpdate = async (form: CreateBookForm) => {
    if (!id) return;
    await updateBook({ id, data: form });
    toast.success('Book updated');
    setShowEdit(false);
  };

  if (isLoading) return (
    <div className="p-8 animate-pulse max-w-3xl">
      <div className="h-6 bg-ink-100 rounded w-24 mb-8" />
      <div className="flex gap-8">
        <div className="w-40 aspect-[2/3] bg-ink-100 rounded-book shadow-book flex-shrink-0" />
        <div className="flex-1 space-y-3">
          <div className="h-8 bg-ink-100 rounded w-3/4" />
          <div className="h-4 bg-ink-100 rounded w-1/2" />
          <div className="h-4 bg-ink-100 rounded w-1/3" />
        </div>
      </div>
    </div>
  );

  if (!book) return (
    <div className="p-8 text-center">
      <p className="font-serif text-xl text-ink-400">Book not found</p>
      <button onClick={() => navigate('/books')} className="mt-4 text-sm text-ochre font-sans hover:underline">Back to catalogue</button>
    </div>
  );

  return (
    <div className="p-8 max-w-3xl">
      {/* Back */}
      <button
        onClick={() => navigate('/books')}
        className="flex items-center gap-1.5 text-sm text-ink-400 font-sans hover:text-ink-900 transition-colors mb-8"
      >
        <ArrowLeft size={14} />
        Back to catalogue
      </button>

      <div className="flex gap-8">
        {/* Cover */}
        <div className="w-40 flex-shrink-0">
          <div className="aspect-[2/3] bg-ink-100 rounded-book shadow-book overflow-hidden">
            {book.coverUrl ? (
              <img src={book.coverUrl} alt={book.title} className="w-full h-full object-cover" />
            ) : (
              <div className="w-full h-full flex items-center justify-center bg-gradient-to-br from-ink-800 to-ink-900 p-4">
                <p className="font-serif text-paper text-xs text-center leading-relaxed">{book.title}</p>
              </div>
            )}
          </div>
        </div>

        {/* Info */}
        <div className="flex-1 min-w-0">
          {/* Genre + status */}
          <div className="flex items-center gap-2 mb-3">
            {book.genre && (
              <span className="text-xs bg-slate-light text-slate-dark px-2 py-0.5 rounded-full font-sans border border-slate/20">
                {book.genre}
              </span>
            )}
            <span className={clsx(
              'text-xs px-2 py-0.5 rounded-full font-sans border',
              book.status === 'available'
                ? 'bg-sage-light text-sage-dark border-sage/20'
                : 'bg-ink-100 text-ink-500 border-ink-200'
            )}>
              {book.status === 'available' ? 'Available' : 'Checked out'}
            </span>
          </div>

          <h1 className="font-serif text-2xl text-ink-900 leading-tight mb-1">{book.title}</h1>
          <p className="text-ink-500 font-sans text-sm mb-1">by <span className="text-ink-700">{book.author}</span></p>
          {book.publishedYear && <p className="text-xs text-ink-300 font-sans mb-4">{book.publishedYear}</p>}
          {book.isbn          && <p className="text-xs text-ink-300 font-sans mb-4 font-mono">ISBN: {book.isbn}</p>}

          {/* Description */}
          <p className="text-sm text-ink-600 font-sans leading-relaxed mb-4">
            {book.description ?? 'No description available.'}
          </p>

          {/* AI generated description */}
          {aiDesc && (
            <div className="mb-4 p-3 bg-ochre-light border border-ochre/20 rounded-sm">
              <div className="flex items-center gap-1.5 text-xs text-ochre-dark font-sans font-medium mb-1.5">
                <Sparkles size={11} />
                AI-generated description
              </div>
              <p className="text-sm text-ink-700 font-sans leading-relaxed">{aiDesc.description}</p>
              {aiDesc.suggestedGenres.length > 0 && (
                <p className="text-xs text-ink-400 font-sans mt-1.5">
                  Suggested genres: {aiDesc.suggestedGenres.join(', ')}
                </p>
              )}
            </div>
          )}

          {/* Actions */}
          <div className="flex flex-wrap gap-2">
            {book.status === 'available' && !activeLoan && (
              <button
                onClick={handleCheckOut}
                disabled={checkingOut}
                className="flex items-center gap-2 px-4 py-2 bg-ink-900 text-paper text-sm font-sans rounded-sm hover:bg-ink-700 transition-colors disabled:opacity-60"
              >
                <BookMarked size={14} />
                {checkingOut ? 'Checking out…' : 'Check out'}
              </button>
            )}

            {activeLoan && (
              <div className="px-4 py-2 bg-sage-light border border-sage/20 text-sage-dark text-sm font-sans rounded-sm">
                You have this book · due {new Date(activeLoan.dueDate).toLocaleDateString()}
              </div>
            )}

            {isLibrarian && (
              <>
                <button
                  onClick={handleAiDescribe}
                  disabled={aiLoading}
                  className="flex items-center gap-2 px-3 py-2 border border-ochre/30 text-ochre-dark bg-ochre-light text-sm font-sans rounded-sm hover:bg-ochre/20 transition-colors disabled:opacity-60"
                >
                  <Sparkles size={13} />
                  {aiLoading ? 'Generating…' : 'AI describe'}
                </button>
                <button
                  onClick={() => setShowEdit(true)}
                  className="flex items-center gap-2 px-3 py-2 border border-ink-200 text-ink-600 text-sm font-sans rounded-sm hover:bg-ink-100 transition-colors"
                >
                  <Pencil size={13} />
                  Edit
                </button>
                <button
                  onClick={handleDelete}
                  disabled={deleting}
                  className="flex items-center gap-2 px-3 py-2 border border-red-200 text-red-600 text-sm font-sans rounded-sm hover:bg-red-50 transition-colors"
                >
                  <Trash2 size={13} />
                  Delete
                </button>
              </>
            )}
          </div>
        </div>
      </div>

      {showEdit && (
        <BookFormModal
          initialData={book}
          onClose={() => setShowEdit(false)}
          onSubmit={handleUpdate}
          loading={updating}
        />
      )}
    </div>
  );
}

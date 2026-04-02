import { useState } from 'react';
import { X, Sparkles } from 'lucide-react';
import { useAiDescribe } from '../../hooks/useApi';
import type { Book, CreateBookForm } from '../../types';

interface Props {
  initialData?: Book;
  onClose: () => void;
  onSubmit: (form: CreateBookForm) => Promise<void>;
  loading: boolean;
}

export function BookFormModal({ initialData, onClose, onSubmit, loading }: Props) {
  const { mutateAsync: aiDescribe, isPending: aiLoading } = useAiDescribe();
  const [form, setForm] = useState<CreateBookForm>({
    title:         initialData?.title ?? '',
    author:        initialData?.author ?? '',
    isbn:          initialData?.isbn ?? '',
    genre:         initialData?.genre ?? '',
    publishedYear: initialData?.publishedYear,
    description:   initialData?.description ?? '',
    coverUrl:      initialData?.coverUrl ?? '',
  });
  const [aiGenres, setAiGenres] = useState<string[]>([]);

  const set = (k: keyof CreateBookForm, v: string | number | undefined) =>
    setForm(f => ({ ...f, [k]: v }));

  const handleAiDescribe = async () => {
    if (!form.title || !form.author) return;
    const result = await aiDescribe({ title: form.title, author: form.author, isbn: form.isbn });
    setForm(f => ({ ...f, description: result.description }));
    setAiGenres(result.suggestedGenres);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await onSubmit(form);
  };

  return (
    <div className="fixed inset-0 bg-ink-900/50 flex items-center justify-center z-50 p-4">
      <div className="bg-paper rounded-sm w-full max-w-lg shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-ink-100">
          <h2 className="font-serif text-xl text-ink-900">
            {initialData ? 'Edit book' : 'Add book'}
          </h2>
          <button onClick={onClose} className="text-ink-400 hover:text-ink-900">
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <Field label="Title *" required>
              <input required value={form.title} onChange={e => set('title', e.target.value)}
                className="input" placeholder="Book title" />
            </Field>
            <Field label="Author *" required>
              <input required value={form.author} onChange={e => set('author', e.target.value)}
                className="input" placeholder="Author name" />
            </Field>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <Field label="ISBN">
              <input value={form.isbn ?? ''} onChange={e => set('isbn', e.target.value)}
                className="input" placeholder="978…" />
            </Field>
            <Field label="Genre">
              <input value={form.genre ?? ''} onChange={e => set('genre', e.target.value)}
                className="input" placeholder="Fiction…" />
            </Field>
            <Field label="Year">
              <input type="number" value={form.publishedYear ?? ''} onChange={e => set('publishedYear', e.target.value ? Number(e.target.value) : undefined)}
                className="input" placeholder="2024" min={1000} max={2099} />
            </Field>
          </div>

          <Field label="Cover URL">
            <input value={form.coverUrl ?? ''} onChange={e => set('coverUrl', e.target.value)}
              className="input" placeholder="https://…" />
          </Field>

          <Field
            label="Description"
            action={
              <button type="button" onClick={handleAiDescribe} disabled={aiLoading || !form.title || !form.author}
                className="flex items-center gap-1 text-xs text-ochre hover:text-ochre-dark disabled:opacity-50">
                <Sparkles size={11} />
                {aiLoading ? 'Generating…' : 'AI generate'}
              </button>
            }
          >
            <textarea value={form.description ?? ''} onChange={e => set('description', e.target.value)}
              rows={3} className="input resize-none" placeholder="Book description…" />
          </Field>

          {/* AI genre suggestions */}
          {aiGenres.length > 0 && (
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-xs text-ink-400 font-sans">Suggested genres:</span>
              {aiGenres.map(g => (
                <button key={g} type="button" onClick={() => set('genre', g)}
                  className="text-xs px-2 py-0.5 bg-ochre-light text-ochre-dark border border-ochre/20 rounded-full font-sans hover:bg-ochre/20 transition-colors">
                  {g}
                </button>
              ))}
            </div>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={onClose}
              className="px-4 py-2 border border-ink-200 text-ink-600 text-sm font-sans rounded-sm hover:bg-ink-100 transition-colors">
              Cancel
            </button>
            <button type="submit" disabled={loading}
              className="px-4 py-2 bg-ink-900 text-paper text-sm font-sans rounded-sm hover:bg-ink-700 transition-colors disabled:opacity-60">
              {loading ? 'Saving…' : initialData ? 'Update book' : 'Add book'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function Field({ label, children, required, action }: {
  label: string; children: React.ReactNode; required?: boolean; action?: React.ReactNode;
}) {
  return (
    <div>
      <div className="flex items-center justify-between mb-1">
        <label className="text-xs font-sans text-ink-500">
          {label}{required && <span className="text-red-400 ml-0.5">*</span>}
        </label>
        {action}
      </div>
      {children}
    </div>
  );
}

import { useNavigate } from 'react-router-dom';
import { BookMarked, RotateCcw, AlertCircle, Clock } from 'lucide-react';
import { clsx } from 'clsx';
import { differenceInDays, format } from 'date-fns';
import { useMyLoans, useMyLoanHistory, useCheckIn } from '../hooks/useApi';
import toast from 'react-hot-toast';
import type { Loan } from '../types';

export function MyLoans() {
  const { data: active,  isLoading: activeLoading }  = useMyLoans();
  const { data: history, isLoading: historyLoading } = useMyLoanHistory();
  const { mutateAsync: checkIn, isPending } = useCheckIn();
  const navigate = useNavigate();

  const handleCheckIn = async (loan: Loan) => {
    await checkIn(loan.id);
    toast.success(`"${loan.book?.title}" returned!`);
  };

  return (
    <div className="p-8 max-w-3xl">
      <h1 className="font-serif text-3xl text-ink-900 mb-8">My loans</h1>

      {/* Active loans */}
      <section className="mb-10">
        <h2 className="font-serif text-lg text-ink-700 mb-4">Currently borrowed</h2>
        {activeLoading ? (
          <div className="space-y-3">
            {[0,1].map(i => <div key={i} className="h-20 bg-ink-100 rounded-sm animate-pulse" />)}
          </div>
        ) : active?.length === 0 ? (
          <div className="py-12 text-center border border-dashed border-ink-200 rounded-sm">
            <BookMarked size={32} className="text-ink-200 mx-auto mb-3" />
            <p className="text-ink-400 font-sans text-sm">No active loans · <button onClick={() => navigate('/books')} className="text-ochre hover:underline">browse the catalogue</button></p>
          </div>
        ) : (
          <div className="space-y-3">
            {active?.map(loan => <LoanRow key={loan.id} loan={loan} onCheckIn={handleCheckIn} checkingIn={isPending} />)}
          </div>
        )}
      </section>

      {/* History */}
      <section>
        <h2 className="font-serif text-lg text-ink-700 mb-4">Reading history</h2>
        {historyLoading ? (
          <div className="space-y-2">
            {[0,1,2].map(i => <div key={i} className="h-14 bg-ink-100 rounded-sm animate-pulse" />)}
          </div>
        ) : history?.filter(l => l.status === 'returned').length === 0 ? (
          <p className="text-sm text-ink-400 font-sans italic">Your reading history will appear here.</p>
        ) : (
          <div className="divide-y divide-ink-100 border border-ink-100 rounded-sm overflow-hidden">
            {history?.filter(l => l.status === 'returned').map(loan => (
              <div key={loan.id} className="flex items-center gap-4 px-4 py-3 bg-paper-light hover:bg-ink-50 transition-colors">
                <div className="w-8 h-12 bg-ink-200 rounded-sm overflow-hidden flex-shrink-0">
                  {loan.book?.coverUrl && <img src={loan.book.coverUrl} className="w-full h-full object-cover" />}
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-sans text-ink-900 truncate">{loan.book?.title}</p>
                  <p className="text-xs text-ink-400 font-sans">{loan.book?.author}</p>
                </div>
                <div className="text-right flex-shrink-0">
                  <p className="text-xs text-ink-300 font-sans">{format(new Date(loan.checkedOutAt), 'MMM d, yyyy')}</p>
                  <p className="text-xs text-sage font-sans">Returned</p>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function LoanRow({ loan, onCheckIn, checkingIn }: { loan: Loan; onCheckIn: (l: Loan) => void; checkingIn: boolean }) {
  const daysLeft = differenceInDays(new Date(loan.dueDate), new Date());
  const overdue  = daysLeft < 0;
  const dueSoon  = daysLeft <= 3 && !overdue;

  return (
    <div className={clsx(
      'flex items-center gap-4 p-4 border rounded-sm',
      overdue ? 'border-red-200 bg-red-50' : dueSoon ? 'border-ochre/30 bg-ochre-light' : 'border-ink-200 bg-paper-light'
    )}>
      {/* Cover thumbnail */}
      <div className="w-10 h-14 bg-ink-200 rounded-sm overflow-hidden flex-shrink-0">
        {loan.book?.coverUrl && <img src={loan.book.coverUrl} className="w-full h-full object-cover" />}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-sans font-medium text-ink-900 truncate">{loan.book?.title}</p>
        <p className="text-xs text-ink-400 font-sans">{loan.book?.author}</p>
        <div className={clsx('flex items-center gap-1 mt-1 text-xs font-sans',
          overdue ? 'text-red-600' : dueSoon ? 'text-ochre-dark' : 'text-ink-400')}>
          {overdue   ? <AlertCircle size={11} /> : <Clock size={11} />}
          {overdue
            ? `Overdue by ${Math.abs(daysLeft)} day${Math.abs(daysLeft) !== 1 ? 's' : ''}`
            : daysLeft === 0
            ? 'Due today'
            : `Due ${format(new Date(loan.dueDate), 'MMM d')} (${daysLeft}d left)`}
        </div>
      </div>

      {/* Check in button */}
      <button
        onClick={() => onCheckIn(loan)}
        disabled={checkingIn}
        className="flex items-center gap-1.5 px-3 py-2 border border-ink-300 text-ink-700 text-xs font-sans rounded-sm hover:bg-ink-100 transition-colors disabled:opacity-60 flex-shrink-0"
      >
        <RotateCcw size={12} />
        Return
      </button>
    </div>
  );
}

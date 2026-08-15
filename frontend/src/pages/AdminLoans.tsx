import { useState } from 'react';
import { AlertCircle, BookMarked, RotateCcw } from 'lucide-react';
import { format, differenceInDays } from 'date-fns';
import { clsx } from 'clsx';
import { useAllActiveLoans, useOverdueLoans, useCheckIn, runMutation } from '../hooks/useApi';
import toast from 'react-hot-toast';
import type { Loan } from '../types';

export function AdminLoans() {
  const [tab, setTab] = useState<'active' | 'overdue'>('active');
  const { data: active,  isLoading: activeLoading  } = useAllActiveLoans();
  const { data: overdue, isLoading: overdueLoading } = useOverdueLoans();
  const { mutateAsync: checkIn, isPending } = useCheckIn();

  const handleCheckIn = async (loan: Loan) => {
    const returned = await runMutation(checkIn(loan.id));
    if (returned) toast.success(`"${loan.book?.title}" checked in`);
  };

  const loans     = tab === 'active' ? active : overdue;
  const isLoading = tab === 'active' ? activeLoading : overdueLoading;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-4xl">
      <div className="flex items-start justify-between gap-3 mb-6">
        <h1 className="font-serif text-2xl sm:text-3xl text-ink-900">Manage loans</h1>
        {overdue && overdue.length > 0 && (
          <div className="flex items-center gap-2 text-xs font-sans text-red-600 bg-red-50 border border-red-200 px-3 py-1.5 rounded-sm flex-shrink-0">
            <AlertCircle size={13} />
            <span className="whitespace-nowrap">
              {overdue.length} overdue<span className="hidden sm:inline"> loan{overdue.length !== 1 ? 's' : ''}</span>
            </span>
          </div>
        )}
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-1 mb-6 border-b border-ink-100">
        {(['active', 'overdue'] as const).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={clsx(
              'px-4 py-2.5 text-sm font-sans border-b-2 -mb-px transition-colors capitalize',
              tab === t ? 'border-ink-900 text-ink-900 font-medium' : 'border-transparent text-ink-400 hover:text-ink-700'
            )}
          >
            {t}
            {t === 'overdue' && overdue && overdue.length > 0 && (
              <span className="ml-2 bg-red-500 text-white text-xs px-1.5 py-0.5 rounded-full">{overdue.length}</span>
            )}
          </button>
        ))}
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="space-y-2">
          {[0,1,2,3].map(i => <div key={i} className="h-16 bg-ink-100 rounded-sm animate-pulse" />)}
        </div>
      ) : !loans?.length ? (
        <div className="py-16 text-center border border-dashed border-ink-200 rounded-sm">
          <BookMarked size={32} className="text-ink-200 mx-auto mb-3" />
          <p className="text-ink-400 font-sans text-sm">No {tab} loans</p>
        </div>
      ) : (
        <>
        {/* Mobile: one card per loan. A six-column table can only be made to fit a phone by
            scrolling it sideways, which hides the check-in action off-screen. */}
        <div className="md:hidden space-y-3">
          {loans.map(loan => {
            const daysLeft = differenceInDays(new Date(loan.dueDate), new Date());
            const isOverdue = daysLeft < 0;
            return (
              <div
                key={loan.id}
                className={clsx(
                  'border rounded-sm p-3',
                  isOverdue ? 'border-red-200 bg-red-50/50' : 'border-ink-200 bg-paper-light'
                )}
              >
                <div className="flex items-start gap-3">
                  <div className="w-10 h-14 bg-ink-200 rounded-sm overflow-hidden flex-shrink-0">
                    {loan.book?.coverUrl && <img src={loan.book.coverUrl} className="w-full h-full object-cover" />}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-sans font-medium text-ink-900 line-clamp-2">{loan.book?.title}</p>
                    <p className="text-xs text-ink-500 font-sans truncate mt-0.5">{loan.userEmail}</p>
                    <span className={clsx(
                      'inline-block mt-2 text-xs px-2 py-0.5 rounded-full border',
                      isOverdue ? 'bg-red-100 text-red-700 border-red-200' : 'bg-sage-light text-sage-dark border-sage/20'
                    )}>
                      {isOverdue ? 'Overdue' : 'Active'}
                    </span>
                  </div>
                </div>

                <dl className="mt-3 grid grid-cols-2 gap-2 text-xs font-sans">
                  <div>
                    <dt className="text-ink-400">Checked out</dt>
                    <dd className="text-ink-600 mt-0.5">{format(new Date(loan.checkedOutAt), 'MMM d, yyyy')}</dd>
                  </div>
                  <div>
                    <dt className="text-ink-400">Due date</dt>
                    <dd className={clsx('mt-0.5', isOverdue ? 'text-red-600 font-medium' : 'text-ink-600')}>
                      {format(new Date(loan.dueDate), 'MMM d, yyyy')}
                      {isOverdue && ` (${Math.abs(daysLeft)}d ago)`}
                    </dd>
                  </div>
                </dl>

                <button
                  onClick={() => handleCheckIn(loan)}
                  disabled={isPending}
                  className="mt-3 w-full flex items-center justify-center gap-1.5 px-3 py-2 border border-ink-200 text-ink-600 text-xs rounded-sm hover:bg-ink-100 transition-colors disabled:opacity-60"
                >
                  <RotateCcw size={11} />
                  Check in
                </button>
              </div>
            );
          })}
        </div>

        {/* `md` and up: the full table */}
        <div className="hidden md:block border border-ink-200 rounded-sm overflow-x-auto">
          <table className="w-full text-sm font-sans">
            <thead className="bg-ink-50 border-b border-ink-100">
              <tr>
                <th className="text-left px-4 py-3 text-xs font-medium text-ink-500">Book</th>
                <th className="text-left px-4 py-3 text-xs font-medium text-ink-500">Borrower</th>
                <th className="text-left px-4 py-3 text-xs font-medium text-ink-500">Checked out</th>
                <th className="text-left px-4 py-3 text-xs font-medium text-ink-500">Due date</th>
                <th className="text-left px-4 py-3 text-xs font-medium text-ink-500">Status</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-ink-100">
              {loans.map(loan => {
                const daysLeft = differenceInDays(new Date(loan.dueDate), new Date());
                const isOverdue = daysLeft < 0;
                return (
                  <tr key={loan.id} className={clsx('hover:bg-ink-50 transition-colors', isOverdue && 'bg-red-50/50')}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <div className="w-6 h-9 bg-ink-200 rounded-sm overflow-hidden flex-shrink-0">
                          {loan.book?.coverUrl && <img src={loan.book.coverUrl} className="w-full h-full object-cover" />}
                        </div>
                        <p className="text-ink-900 font-medium line-clamp-1">{loan.book?.title}</p>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-ink-600 text-xs">{loan.userEmail}</td>
                    <td className="px-4 py-3 text-ink-500 text-xs">{format(new Date(loan.checkedOutAt), 'MMM d, yyyy')}</td>
                    <td className="px-4 py-3">
                      <span className={clsx('text-xs', isOverdue ? 'text-red-600 font-medium' : 'text-ink-500')}>
                        {format(new Date(loan.dueDate), 'MMM d, yyyy')}
                        {isOverdue && ` (${Math.abs(daysLeft)}d ago)`}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span className={clsx(
                        'text-xs px-2 py-0.5 rounded-full border',
                        isOverdue ? 'bg-red-100 text-red-700 border-red-200' : 'bg-sage-light text-sage-dark border-sage/20'
                      )}>
                        {isOverdue ? 'Overdue' : 'Active'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        onClick={() => handleCheckIn(loan)}
                        disabled={isPending}
                        className="flex items-center gap-1.5 px-3 py-1.5 border border-ink-200 text-ink-600 text-xs rounded-sm hover:bg-ink-100 transition-colors disabled:opacity-60 ml-auto"
                      >
                        <RotateCcw size={11} />
                        Check in
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        </>
      )}
    </div>
  );
}

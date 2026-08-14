import {
  useQuery, useMutation, useQueryClient,
  type UseMutationOptions,
} from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { api, ApiError } from '../lib/api';
import type {
  Book, Loan, DashboardStats, PagedResult,
  AiDescribeResponse, AiSearchResult, BookRecommendation,
  CreateBookForm,
} from '../types';

/**
 * Every mutation in the app used to be called as a bare `await mutateAsync(...)` followed by
 * a success toast, with no catch anywhere — so a 4xx/5xx skipped the toast, showed the user
 * nothing at all, and escaped to `window.onunhandledrejection`. Wrapping the options here
 * gives all of them a default `onError` that surfaces the server's message, so the status
 * codes the API works hard to distinguish actually reach the user.
 *
 * A caller may still pass its own `onError` to add behaviour; it runs after the toast.
 */
function withErrorToast<TData, TVariables, TContext>(
  options: UseMutationOptions<TData, Error, TVariables, TContext>,
): UseMutationOptions<TData, Error, TVariables, TContext> {
  return {
    ...options,
    // Rest-forwarded rather than destructured: react-query has changed the trailing
    // parameters of this callback across 5.x minors, and the toast only needs the error.
    onError: (...args: Parameters<NonNullable<typeof options.onError>>) => {
      const [error] = args;
      // A 401 already triggers a sign-out and redirect in the api interceptor; a toast on
      // top of that would just be noise on the way to /login.
      if (!(error instanceof ApiError && error.status === 401)) {
        toast.error(error.message || 'Something went wrong');
      }
      options.onError?.(...args);
    },
  };
}

/**
 * `mutateAsync` rejects on failure, which is what made the un-caught `await` calls throw.
 * Callers that only want to run follow-up work on success use this: it resolves to the
 * result, or to `undefined` when the mutation failed (already reported by the toast above).
 */
export async function runMutation<T>(promise: Promise<T>): Promise<T | undefined> {
  try {
    return await promise;
  } catch {
    return undefined;
  }
}

// ── Books ─────────────────────────────────────────────────────────────────────
export function useBooks(
  params: Record<string, string | number | undefined>,
  options?: { enabled?: boolean },
) {
  const q = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => { if (v !== undefined && v !== '') q.set(k, String(v)); });
  return useQuery<PagedResult<Book>>({
    queryKey: ['books', params],
    queryFn: () => api.get(`/api/books?${q}`).then(r => r.data),
    enabled: options?.enabled ?? true,
  });
}

export function useBook(id: string | undefined) {
  return useQuery<Book>({
    queryKey: ['book', id],
    queryFn: () => api.get(`/api/books/${id}`).then(r => r.data),
    enabled: !!id,
  });
}

export function useGenres() {
  return useQuery<string[]>({
    queryKey: ['genres'],
    queryFn: () => api.get('/api/books/genres').then(r => Array.isArray(r.data) ? r.data : []),
    staleTime: 1000 * 60 * 5,
  });
}

export function useStats() {
  return useQuery<DashboardStats>({
    queryKey: ['stats'],
    queryFn: () => api.get('/api/books/stats').then(r => r.data),
    refetchInterval: 30_000,
  });
}

export function useCreateBook() {
  const qc = useQueryClient();
  return useMutation(withErrorToast({
    mutationFn: (data: CreateBookForm) => api.post<Book>('/api/books', data).then(r => r.data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['books'] }); qc.invalidateQueries({ queryKey: ['stats'] }); },
  }));
}

export function useUpdateBook() {
  const qc = useQueryClient();
  return useMutation(withErrorToast({
    mutationFn: ({ id, data }: { id: string; data: Partial<CreateBookForm> }) =>
      api.put<Book>(`/api/books/${id}`, data).then(r => r.data),
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: ['books'] });
      qc.invalidateQueries({ queryKey: ['book', id] });
    },
  }));
}

export function useDeleteBook() {
  const qc = useQueryClient();
  return useMutation(withErrorToast({
    mutationFn: (id: string) => api.delete(`/api/books/${id}`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['books'] }); qc.invalidateQueries({ queryKey: ['stats'] }); },
  }));
}

// ── Loans ─────────────────────────────────────────────────────────────────────
export function useMyLoans() {
  return useQuery<Loan[]>({
    queryKey: ['loans', 'my'],
    queryFn: () => api.get('/api/loans/my').then(r => r.data),
  });
}

export function useMyLoanHistory() {
  return useQuery<Loan[]>({
    queryKey: ['loans', 'my', 'history'],
    queryFn: () => api.get('/api/loans/my/history').then(r => r.data),
  });
}

export function useAllActiveLoans() {
  return useQuery<Loan[]>({
    queryKey: ['loans', 'active'],
    queryFn: () => api.get('/api/loans/active').then(r => r.data),
  });
}

export function useOverdueLoans() {
  return useQuery<Loan[]>({
    queryKey: ['loans', 'overdue'],
    queryFn: () => api.get('/api/loans/overdue').then(r => r.data),
    refetchInterval: 60_000,
  });
}

export function useCheckOut() {
  const qc = useQueryClient();
  return useMutation(withErrorToast({
    mutationFn: (bookId: string) => api.post<Loan>(`/api/loans/checkout/${bookId}`).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['books'] });
      qc.invalidateQueries({ queryKey: ['loans'] });
      qc.invalidateQueries({ queryKey: ['stats'] });
    },
  }));
}

export function useCheckIn() {
  const qc = useQueryClient();
  return useMutation(withErrorToast({
    mutationFn: (loanId: string) => api.post<Loan>(`/api/loans/checkin/${loanId}`).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['books'] });
      qc.invalidateQueries({ queryKey: ['loans'] });
      qc.invalidateQueries({ queryKey: ['stats'] });
    },
  }));
}

// ── AI ────────────────────────────────────────────────────────────────────────
export function useAiDescribe() {
  return useMutation(withErrorToast({
    mutationFn: (data: { title: string; author: string; isbn?: string }) =>
      api.post<AiDescribeResponse>('/api/ai/describe', data).then(r => r.data),
  }));
}

export function useAiSearch() {
  return useMutation(withErrorToast({
    mutationFn: (naturalQuery: string) =>
      api.post<AiSearchResult>('/api/ai/search', { naturalQuery }).then(r => r.data),
  }));
}

export function useAiRecommend() {
  return useQuery<{ recommendations: BookRecommendation[] }>({
    queryKey: ['ai', 'recommend'],
    queryFn: () => api.get('/api/ai/recommend').then(r => r.data),
    staleTime: 1000 * 60 * 10,
  });
}

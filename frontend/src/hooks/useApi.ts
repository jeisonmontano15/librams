import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api';
import type {
  Book, Loan, DashboardStats, PagedResult,
  AiDescribeResponse, AiSearchResult, BookRecommendation,
  CreateBookForm,
} from '../types';

// ── Books ─────────────────────────────────────────────────────────────────────
export function useBooks(params: Record<string, string | number | undefined>) {
  const q = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => { if (v !== undefined && v !== '') q.set(k, String(v)); });
  return useQuery<PagedResult<Book>>({
    queryKey: ['books', params],
    queryFn: () => api.get(`/api/books?${q}`).then(r => r.data),
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
    queryFn: () => api.get('/api/books/genres').then(r => r.data),
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
  return useMutation({
    mutationFn: (data: CreateBookForm) => api.post<Book>('/api/books', data).then(r => r.data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['books'] }); qc.invalidateQueries({ queryKey: ['stats'] }); },
  });
}

export function useUpdateBook() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<CreateBookForm> }) =>
      api.put<Book>(`/api/books/${id}`, data).then(r => r.data),
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: ['books'] });
      qc.invalidateQueries({ queryKey: ['book', id] });
    },
  });
}

export function useDeleteBook() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/api/books/${id}`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['books'] }); qc.invalidateQueries({ queryKey: ['stats'] }); },
  });
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
  return useMutation({
    mutationFn: (bookId: string) => api.post<Loan>(`/api/loans/checkout/${bookId}`).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['books'] });
      qc.invalidateQueries({ queryKey: ['loans'] });
      qc.invalidateQueries({ queryKey: ['stats'] });
    },
  });
}

export function useCheckIn() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (loanId: string) => api.post<Loan>(`/api/loans/checkin/${loanId}`).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['books'] });
      qc.invalidateQueries({ queryKey: ['loans'] });
      qc.invalidateQueries({ queryKey: ['stats'] });
    },
  });
}

// ── AI ────────────────────────────────────────────────────────────────────────
export function useAiDescribe() {
  return useMutation({
    mutationFn: (data: { title: string; author: string; isbn?: string }) =>
      api.post<AiDescribeResponse>('/api/ai/describe', data).then(r => r.data),
  });
}

export function useAiSearch() {
  return useMutation({
    mutationFn: (naturalQuery: string) =>
      api.post<AiSearchResult>('/api/ai/search', { naturalQuery }).then(r => r.data),
  });
}

export function useAiRecommend() {
  return useQuery<{ recommendations: BookRecommendation[] }>({
    queryKey: ['ai', 'recommend'],
    queryFn: () => api.get('/api/ai/recommend').then(r => r.data),
    staleTime: 1000 * 60 * 10,
  });
}

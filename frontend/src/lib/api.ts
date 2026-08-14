import axios, { AxiosError } from 'axios';
import { supabase } from './supabase';

export const api = axios.create({
  baseURL: (import.meta.env.VITE_API_URL as string) ?? '',
});

/**
 * An error carrying the HTTP status alongside a human-readable message, so callers can
 * branch on 401/403/404/409 without re-parsing an axios error.
 */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status?: number,
    /** Field-level messages from an RFC 7807 ValidationProblem, keyed by field name. */
    readonly fieldErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/**
 * The API answers with two different error shapes: `{ error }` for handled failures
 * (409 conflicts, 429, the 500 fallback) and RFC 7807 `{ title, status, errors }` from
 * `Results.ValidationProblem`. Reading only `data.error` meant every validation failure
 * surfaced as axios's opaque "Request failed with status code 400". Both are understood
 * here so the message the user sees is the one the server actually sent.
 */
function toApiError(err: AxiosError): ApiError {
  const status = err.response?.status;
  const data = err.response?.data as
    | { error?: string; title?: string; detail?: string; errors?: Record<string, string[]> }
    | undefined;

  if (data?.errors && typeof data.errors === 'object') {
    const messages = Object.values(data.errors).flat();
    return new ApiError(
      messages.join(' ') || data.title || 'Validation failed',
      status,
      data.errors,
    );
  }

  const message =
    data?.error ??
    data?.detail ??
    data?.title ??
    (status === 401 ? 'Your session has expired. Please sign in again.' : undefined) ??
    (status === 403 ? 'You do not have permission to do that.' : undefined) ??
    (status === 404 ? 'Not found.' : undefined) ??
    err.message ??
    'Request failed';

  return new ApiError(message, status);
}

api.interceptors.request.use(async (config) => {
  const { data } = await supabase.auth.getSession();
  if (data.session?.access_token) {
    config.headers.Authorization = `Bearer ${data.session.access_token}`;
  }
  return config;
});

api.interceptors.response.use(
  (r) => r,
  async (err: AxiosError) => {
    // An expired or revoked token previously left the user on a stale page with no
    // feedback. Sign out so the auth listener fires and `Protected` redirects to /login.
    if (err.response?.status === 401) {
      const { data } = await supabase.auth.getSession();
      if (data.session) await supabase.auth.signOut();
    }
    return Promise.reject(toApiError(err));
  }
);

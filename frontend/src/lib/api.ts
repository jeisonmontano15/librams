import axios from 'axios';
import { supabase } from './supabase';

export const api = axios.create({
  baseURL: (import.meta.env.VITE_API_URL as string) ?? '',
});

api.interceptors.request.use(async (config) => {
  const { data } = await supabase.auth.getSession();
  if (data.session?.access_token) {
    config.headers.Authorization = `Bearer ${data.session.access_token}`;
  }
  return config;
});

api.interceptors.response.use(
  (r) => r,
  (err) => {
    const msg = err.response?.data?.error ?? err.message ?? 'Request failed';
    return Promise.reject(new Error(msg));
  }
);

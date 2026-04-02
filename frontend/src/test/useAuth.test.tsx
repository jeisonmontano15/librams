import { renderHook, act } from '@testing-library/react';
import { vi } from 'vitest';
import { AuthProvider, useAuth } from '../hooks/useAuth';
import { supabase } from '../lib/supabase';

const mockSession = {
  user: { id: 'user-123', email: 'test@example.com' },
  access_token: 'fake-token',
};

vi.mock('../lib/supabase', () => ({
  supabase: {
    auth: {
      getSession: vi.fn(),
      onAuthStateChange: vi.fn(),
      signInWithOAuth: vi.fn(),
      signOut: vi.fn(),
    },
  },
}));

vi.mock('../lib/api', () => ({
  api: {
    get: vi.fn().mockResolvedValue({ data: null }),
  },
}));

describe('useAuth', () => {
  beforeEach(() => {
    vi.mocked(supabase.auth.onAuthStateChange).mockReturnValue({
      data: { subscription: { unsubscribe: vi.fn() } },
    } as any);
  });

  it('returns null user when there is no session', async () => {
    vi.mocked(supabase.auth.getSession).mockResolvedValue({ data: { session: null } } as any);

    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <AuthProvider>{children}</AuthProvider>
    );

    const { result } = renderHook(() => useAuth(), { wrapper });

    await act(async () => {});

    expect(result.current.user).toBeNull();
  });

  it('returns user object when session exists', async () => {
    vi.mocked(supabase.auth.getSession).mockResolvedValue({ data: { session: mockSession } } as any);

    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <AuthProvider>{children}</AuthProvider>
    );

    const { result } = renderHook(() => useAuth(), { wrapper });

    await act(async () => {});

    expect(result.current.user).toEqual(mockSession.user);
  });
});

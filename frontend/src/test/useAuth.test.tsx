import { renderHook, act } from '@testing-library/react';
import { vi } from 'vitest';
import { AuthProvider, useAuth } from '../hooks/useAuth';

const mockSession = {
  user: { id: 'user-123', email: 'test@example.com' },
  access_token: 'fake-token',
};

const mockGetSession = vi.fn();
const mockOnAuthStateChange = vi.fn();

vi.mock('../lib/supabase', () => ({
  supabase: {
    auth: {
      getSession: mockGetSession,
      onAuthStateChange: mockOnAuthStateChange,
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
    mockOnAuthStateChange.mockReturnValue({
      data: { subscription: { unsubscribe: vi.fn() } },
    });
  });

  it('returns null user when there is no session', async () => {
    mockGetSession.mockResolvedValue({ data: { session: null } });

    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <AuthProvider>{children}</AuthProvider>
    );

    const { result } = renderHook(() => useAuth(), { wrapper });

    await act(async () => {});

    expect(result.current.user).toBeNull();
  });

  it('returns user object when session exists', async () => {
    mockGetSession.mockResolvedValue({ data: { session: mockSession } });

    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <AuthProvider>{children}</AuthProvider>
    );

    const { result } = renderHook(() => useAuth(), { wrapper });

    await act(async () => {});

    expect(result.current.user).toEqual(mockSession.user);
  });
});

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { BookFormModal } from '../components/books/BookFormModal';

vi.mock('../hooks/useApi', () => ({
  useAiDescribe: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

vi.mock('../lib/supabase', () => ({
  supabase: {
    auth: {
      getSession: vi.fn().mockResolvedValue({ data: { session: null } }),
      onAuthStateChange: vi.fn().mockReturnValue({ data: { subscription: { unsubscribe: vi.fn() } } }),
    },
  },
}));

describe('BookFormModal', () => {
  it('calls onSubmit with form values when submitted with a title', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const onClose = vi.fn();

    render(
      <BookFormModal onClose={onClose} onSubmit={onSubmit} loading={false} />
    );

    fireEvent.change(screen.getByPlaceholderText('Book title'), {
      target: { value: 'My Test Book' },
    });
    fireEvent.change(screen.getByPlaceholderText('Author name'), {
      target: { value: 'Some Author' },
    });

    fireEvent.click(screen.getByRole('button', { name: 'Add book' }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'My Test Book', author: 'Some Author' })
      );
    });
  });
});

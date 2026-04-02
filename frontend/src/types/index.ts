export type BookStatus = 'available' | 'checked_out';
export type LoanStatus = 'active' | 'returned' | 'overdue';
export type UserRole   = 'librarian' | 'member';

export interface Book {
  id: string;
  title: string;
  author: string;
  isbn?: string;
  genre?: string;
  publishedYear?: number;
  description?: string;
  coverUrl?: string;
  status: BookStatus;
  createdAt: string;
  updatedAt: string;
}

export interface Loan {
  id: string;
  bookId: string;
  userId: string;
  userEmail: string;
  checkedOutAt: string;
  dueDate: string;
  returnedAt?: string;
  status: LoanStatus;
  book?: Book;
}

export interface LibraryUser {
  id: string;
  email: string;
  displayName?: string;
  role: UserRole;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface DashboardStats {
  totalBooks: number;
  available: number;
  checkedOut: number;
  overdue: number;
  totalLoans: number;
}

export interface AiDescribeResponse {
  description: string;
  suggestedGenres: string[];
}

export interface AiSearchResult {
  parsed: { query?: string; genre?: string; status?: BookStatus; explanation: string };
  results: PagedResult<Book>;
}

export interface BookRecommendation {
  title: string;
  author: string;
  reason: string;
  matchedBookId?: string;
}

export interface CreateBookForm {
  title: string;
  author: string;
  isbn?: string;
  genre?: string;
  publishedYear?: number;
  description?: string;
  coverUrl?: string;
}

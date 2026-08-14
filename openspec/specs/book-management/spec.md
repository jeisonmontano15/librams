# book-management Specification

## Purpose
Defines the lifecycle of a book record in the catalogue: creation, editing, and deletion by librarians, the validation those operations must pass, how availability is persisted, and public retrieval of individual books and dashboard statistics.
## Requirements
### Requirement: Librarians can add books to the catalogue
The system SHALL allow users holding the `librarian` role to create books via `POST /api/books`, persisting title, author, ISBN, genre, published year, description, and cover URL. Newly created books SHALL default to `available` status.

#### Scenario: Librarian creates a book with full metadata
- **WHEN** a librarian sends `POST /api/books` with a valid title, author, ISBN, genre, published year, description, and cover URL
- **THEN** the response status is `201 Created` with a `Location` header pointing at the new book and the response body contains the persisted book including its generated ID

#### Scenario: Librarian creates a book with only required fields
- **WHEN** a librarian sends `POST /api/books` with only title and author
- **THEN** the response status is `201 Created` and the optional fields are stored as null

#### Scenario: New book defaults to available
- **WHEN** a book is created without an explicit status
- **THEN** the persisted book has status `available`

#### Scenario: Member cannot create a book
- **WHEN** a user holding the `member` role sends `POST /api/books`
- **THEN** the response status is `403 Forbidden` and no book is created

#### Scenario: Anonymous request cannot create a book
- **WHEN** an unauthenticated client sends `POST /api/books`
- **THEN** the response status is `401 Unauthorized`

### Requirement: Create book request is validated
The system SHALL validate `CreateBookRequest` using FluentValidation, requiring a non-empty title of at most 300 characters and a non-empty author of at most 200 characters, limiting ISBN to 20 characters when provided, and constraining published year to between 1000 and the current year plus one when provided.

#### Scenario: Missing title is rejected
- **WHEN** a librarian sends `POST /api/books` with an empty title
- **THEN** the response status is `400 Bad Request` with a validation error body naming the title field

#### Scenario: Missing author is rejected
- **WHEN** a librarian sends `POST /api/books` with an empty author
- **THEN** the response status is `400 Bad Request` with a validation error body naming the author field

#### Scenario: Published year in the far future is rejected
- **WHEN** a librarian sends `POST /api/books` with a published year greater than the current year plus one
- **THEN** the response status is `400 Bad Request` with a validation error body

### Requirement: Librarians can edit books
The system SHALL allow librarians to update a book via `PUT /api/books/:id`. Updates SHALL be partial: fields omitted from the request retain their existing stored values.

#### Scenario: Partial update leaves omitted fields untouched
- **WHEN** a librarian sends `PUT /api/books/:id` setting only the genre
- **THEN** the response status is `200 OK`, the genre is updated, and title, author, ISBN, published year, description, and cover URL retain their previous values

#### Scenario: Updating an unknown book returns 404
- **WHEN** a librarian sends `PUT /api/books/:id` for an ID that does not exist
- **THEN** the response status is `404 Not Found`

#### Scenario: Member cannot edit a book
- **WHEN** a user holding the `member` role sends `PUT /api/books/:id`
- **THEN** the response status is `403 Forbidden` and the book is unchanged

### Requirement: Book status persisted correctly
The system SHALL map `BookStatus.Available` to the string `"available"` and `BookStatus.CheckedOut` to the string `"checked_out"` when reading from or writing to the database. The previous implementation incorrectly produced `"checkedout"` (without underscore), which caused silent failures in status filtering and checkout operations.

#### Scenario: Available book status round-trips correctly
- **WHEN** a book is created and then retrieved from the database
- **THEN** its status is `BookStatus.Available` and the raw DB column value is `"available"`

#### Scenario: CheckedOut book status round-trips correctly
- **WHEN** a book's status is set to `BookStatus.CheckedOut` via `SetStatusAsync`
- **THEN** the raw DB column value is `"checked_out"` (with underscore)

#### Scenario: Status filter returns only available books
- **WHEN** `SearchAsync` is called with `status = BookStatus.Available`
- **THEN** only books whose DB `status` column is `"available"` are returned

#### Scenario: Status filter returns only checked-out books
- **WHEN** `SearchAsync` is called with `status = BookStatus.CheckedOut`
- **THEN** only books whose DB `status` column is `"checked_out"` are returned

### Requirement: Librarians can delete books
The system SHALL allow librarians to delete a book via `DELETE /api/books/:id`. Deleting a book SHALL cascade to remove its associated loan records.

#### Scenario: Deleting an existing book succeeds
- **WHEN** a librarian sends `DELETE /api/books/:id` for an existing book
- **THEN** the response status is `204 No Content` and the book is no longer retrievable

#### Scenario: Deleting an unknown book returns 404
- **WHEN** a librarian sends `DELETE /api/books/:id` for an ID that does not exist
- **THEN** the response status is `404 Not Found`

#### Scenario: Member cannot delete a book
- **WHEN** a user holding the `member` role sends `DELETE /api/books/:id`
- **THEN** the response status is `403 Forbidden` and the book still exists

### Requirement: Anyone can retrieve a single book
The system SHALL expose `GET /api/books/:id` without requiring authentication, returning the full book record.

#### Scenario: Retrieving an existing book returns its details
- **WHEN** a client requests `GET /api/books/:id` for an existing book
- **THEN** the response status is `200 OK` and the body contains the book's title, author, status, and metadata

#### Scenario: Retrieving an unknown book returns 404
- **WHEN** a client requests `GET /api/books/:id` for an ID that does not exist
- **THEN** the response status is `404 Not Found`

### Requirement: Dashboard statistics are available
The system SHALL expose `GET /api/books/stats` to any authenticated user, returning the total number of books, the count available, the count checked out, the count of overdue loans, and the total number of loans.

#### Scenario: Statistics reflect current catalogue state
- **WHEN** an authenticated user requests `GET /api/books/stats`
- **THEN** the response status is `200 OK` and the body contains `totalBooks`, `available`, `checkedOut`, `overdue`, and `totalLoans`

#### Scenario: Anonymous request for statistics is rejected
- **WHEN** an unauthenticated client requests `GET /api/books/stats`
- **THEN** the response status is `401 Unauthorized`

### Requirement: Update book request is validated
The system SHALL validate `UpdateBookRequest` on `PUT /api/books/:id` using FluentValidation, rejecting requests where provided fields violate the same length and range constraints as `CreateBookRequest`.

#### Scenario: PUT with valid partial update succeeds
- **WHEN** a librarian sends a PUT request with only `title` set to a non-empty string under 300 characters
- **THEN** the response status is `200 OK` and the book is updated

#### Scenario: PUT with title exceeding max length returns 400
- **WHEN** a librarian sends a PUT request with `title` set to a string longer than 300 characters
- **THEN** the response status is `400 Bad Request` with a validation error body

#### Scenario: PUT with invalid published year returns 400
- **WHEN** a librarian sends a PUT request with `publishedYear` set to `999` (below 1000)
- **THEN** the response status is `400 Bad Request` with a validation error body


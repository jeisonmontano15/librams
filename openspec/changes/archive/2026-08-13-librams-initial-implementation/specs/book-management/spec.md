## ADDED Requirements

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
The system SHALL persist a book's availability as one of two stored values, `available` or `checked_out`, and SHALL map those stored values back to the corresponding status when reading. Status filtering SHALL match against the stored representation.

#### Scenario: Available book status round-trips correctly
- **WHEN** a book is created and then retrieved from the database
- **THEN** its status is available and the raw DB column value is `"available"`

#### Scenario: Status filter returns only available books
- **WHEN** the catalogue is searched with a status filter of available
- **THEN** only books whose stored status column is `"available"` are returned

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

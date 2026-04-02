## MODIFIED Requirements

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

## ADDED Requirements

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

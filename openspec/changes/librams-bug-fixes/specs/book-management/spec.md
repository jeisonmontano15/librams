## MODIFIED Requirements

### Requirement: Librarians can edit books
The system SHALL allow librarians to update a book via `PUT /api/books/:id`. Updates SHALL be partial: fields omitted from the request retain their existing stored values. For the optional descriptive fields — genre, description, and cover URL — a librarian SHALL additionally be able to clear a previously stored value by submitting it as empty. Title and author are required and SHALL NOT be clearable.

#### Scenario: Partial update leaves omitted fields untouched
- **WHEN** a librarian sends `PUT /api/books/:id` setting only the genre
- **THEN** the response status is `200 OK`, the genre is updated, and title, author, ISBN, published year, description, and cover URL retain their previous values

#### Scenario: Submitting an empty description clears it
- **WHEN** a librarian sends `PUT /api/books/:id` with the description set to an empty value for a book that currently has a description
- **THEN** the response status is `200 OK` and the stored description is cleared

#### Scenario: Submitting an empty genre clears it
- **WHEN** a librarian sends `PUT /api/books/:id` with the genre set to an empty value for a book that currently has a genre
- **THEN** the stored genre is cleared and the book no longer appears under its former genre filter

#### Scenario: Submitting an empty cover URL clears it
- **WHEN** a librarian sends `PUT /api/books/:id` with the cover URL set to an empty value for a book that currently has cover artwork
- **THEN** the stored cover URL is cleared and the catalogue falls back to the placeholder

#### Scenario: Omitting a clearable field still preserves it
- **WHEN** a librarian sends `PUT /api/books/:id` that does not mention the description at all
- **THEN** the existing description is preserved unchanged

#### Scenario: Updating an unknown book returns 404
- **WHEN** a librarian sends `PUT /api/books/:id` for an ID that does not exist
- **THEN** the response status is `404 Not Found`

#### Scenario: Member cannot edit a book
- **WHEN** a user holding the `member` role sends `PUT /api/books/:id`
- **THEN** the response status is `403 Forbidden` and the book is unchanged

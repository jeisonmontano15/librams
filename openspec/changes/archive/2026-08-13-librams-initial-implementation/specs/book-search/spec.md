## ADDED Requirements

### Requirement: Books are searchable by title, author, and description
The system SHALL provide full-text search over the catalogue via the `query` parameter on `GET /api/books`, matching against book title, author, and description using PostgreSQL full-text search. A GIN index SHALL back this search.

#### Scenario: Search matches a book by title
- **WHEN** a client requests `GET /api/books?query=<word from a book title>`
- **THEN** the response contains that book in its items

#### Scenario: Search matches a book by author
- **WHEN** a client requests `GET /api/books?query=<author surname>`
- **THEN** the response contains books written by that author

#### Scenario: Search matches a book by description
- **WHEN** a client requests `GET /api/books?query=<word appearing only in a description>`
- **THEN** the response contains the book whose description contains that word

#### Scenario: Search with no matches returns an empty result
- **WHEN** a client requests `GET /api/books?query=<term matching no book>`
- **THEN** the response status is `200 OK`, items is empty, and total is `0`

#### Scenario: Search is available without authentication
- **WHEN** an unauthenticated client requests `GET /api/books?query=<term>`
- **THEN** the response status is `200 OK`

### Requirement: Results can be filtered by genre
The system SHALL filter catalogue results by genre via the `genre` parameter on `GET /api/books`, matching case-insensitively.

#### Scenario: Genre filter returns only matching books
- **WHEN** a client requests `GET /api/books?genre=Fiction`
- **THEN** every returned book has a genre matching `Fiction`

#### Scenario: Genre filter is case-insensitive
- **WHEN** a client requests `GET /api/books?genre=fiction`
- **THEN** books whose stored genre is `Fiction` are returned

### Requirement: Results can be filtered by availability
The system SHALL filter catalogue results by availability via the `status` parameter on `GET /api/books`, accepting `available` and `checked_out`. An unrecognised status value SHALL be ignored rather than rejected.

#### Scenario: Filtering by available returns only available books
- **WHEN** a client requests `GET /api/books?status=available`
- **THEN** every returned book has status `available`

#### Scenario: Filtering by checked_out returns only borrowed books
- **WHEN** a client requests `GET /api/books?status=checked_out`
- **THEN** every returned book has status `checked_out`

#### Scenario: Unrecognised status value is ignored
- **WHEN** a client requests `GET /api/books?status=nonsense`
- **THEN** the response status is `200 OK` and results are returned without a status filter applied

### Requirement: Search filters combine
The system SHALL apply query, genre, and status filters together when more than one is supplied, returning only books satisfying every supplied filter.

#### Scenario: Query and status filters apply together
- **WHEN** a client requests `GET /api/books?query=<term>&status=available`
- **THEN** every returned book matches the search term and has status `available`

#### Scenario: Genre and status filters apply together
- **WHEN** a client requests `GET /api/books?genre=Fiction&status=available`
- **THEN** every returned book has genre `Fiction` and status `available`

### Requirement: Results are paginated
The system SHALL paginate catalogue results, defaulting to page 1 with a page size of 20. The response SHALL report the total number of matching books alongside the current page and page size, so clients can compute the page count.

#### Scenario: Default pagination applies when unspecified
- **WHEN** a client requests `GET /api/books` with no paging parameters
- **THEN** at most 20 books are returned, page is `1`, and pageSize is `20`

#### Scenario: Requesting a later page returns the next slice
- **WHEN** a client requests `GET /api/books?page=2&pageSize=10` against a catalogue of more than 10 books
- **THEN** the returned items differ from those on page 1 and total reports the full match count

#### Scenario: Total reflects all matches, not just the current page
- **WHEN** a client requests a page of results for a filter matching more books than the page size
- **THEN** total is greater than the number of items returned

### Requirement: Available genres can be listed
The system SHALL expose `GET /api/books/genres` without requiring authentication, returning the distinct non-null genres present in the catalogue in alphabetical order.

#### Scenario: Genre list contains distinct values in order
- **WHEN** a client requests `GET /api/books/genres`
- **THEN** the response status is `200 OK` and the body is an alphabetically ordered list of distinct genres containing no duplicates and no null entries

## MODIFIED Requirements

### Requirement: Results can be filtered by genre
The system SHALL filter catalogue results by genre via the `genre` parameter on `GET /api/books`, matching the stored genre in full and case-insensitively. Partial matches SHALL NOT be returned: a genre whose name contains the requested genre as a substring is a different genre and SHALL be excluded.

#### Scenario: Genre filter returns only matching books
- **WHEN** a client requests `GET /api/books?genre=Fiction`
- **THEN** every returned book has a genre matching `Fiction`

#### Scenario: Genre filter is case-insensitive
- **WHEN** a client requests `GET /api/books?genre=fiction`
- **THEN** books whose stored genre is `Fiction` are returned

#### Scenario: Genre filter excludes substring matches
- **WHEN** a client requests `GET /api/books?genre=Fiction` against a catalogue also holding books with genres `Non-Fiction` and `Science Fiction`
- **THEN** only books whose genre is exactly `Fiction` are returned, and the `Non-Fiction` and `Science Fiction` books are excluded

#### Scenario: Filtering by a compound genre returns that genre
- **WHEN** a client requests `GET /api/books?genre=Science Fiction`
- **THEN** only books whose genre is exactly `Science Fiction` are returned

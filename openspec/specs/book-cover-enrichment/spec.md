# book-cover-enrichment Specification

## Purpose
Defines how book cover artwork is sourced from the external Open Library service by ISBN, and requires that failures of that optional service never block catalogue operations.
## Requirements
### Requirement: Book covers are sourced from Open Library
The system SHALL look up book cover images and metadata from the Open Library service by ISBN, so that catalogue entries can display artwork without librarians uploading files.

#### Scenario: Cover is found for a known ISBN
- **WHEN** a lookup is performed for an ISBN present in Open Library
- **THEN** a cover image URL is returned for storage against the book

#### Scenario: Unknown ISBN yields no cover
- **WHEN** a lookup is performed for an ISBN that Open Library does not recognise
- **THEN** no cover URL is returned and the book is stored without one

### Requirement: Cover lookup failures are non-fatal
The system SHALL treat the external cover service as optional. A failure or timeout when contacting it SHALL NOT prevent a book from being created, edited, or displayed.

#### Scenario: Book creation succeeds when the cover service is unavailable
- **WHEN** a librarian creates a book while the Open Library service is unreachable
- **THEN** the book is created successfully without a cover URL

#### Scenario: Catalogue renders without a cover
- **WHEN** a book has no cover URL
- **THEN** the catalogue displays a readable placeholder carrying the book's title in place of artwork


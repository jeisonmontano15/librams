## ADDED Requirements

### Requirement: Natural language catalogue search
The system SHALL accept a free-text query at `POST /api/ai/search` from any authenticated user, use a language model to translate it into structured catalogue filters (search term, genre, availability), run the resulting search, and return both the interpreted filters and the matching books. The model SHALL be constrained to genres that actually exist in the catalogue.

#### Scenario: Availability phrasing becomes a status filter
- **WHEN** an authenticated user searches for a phrase indicating they want only borrowable books, such as "sci-fi I can borrow right now"
- **THEN** the interpreted filters carry status `available` and every returned book is available

#### Scenario: Genre phrasing becomes a genre filter
- **WHEN** a user submits a query naming a genre present in the catalogue
- **THEN** the interpreted filters carry that genre and the returned books belong to it

#### Scenario: Interpretation is returned alongside results
- **WHEN** a user submits any natural language query
- **THEN** the response contains both the interpreted filters with a short explanation of what was understood and the matching books

#### Scenario: Anonymous request is rejected
- **WHEN** an unauthenticated client calls `POST /api/ai/search`
- **THEN** the response status is `401 Unauthorized`

### Requirement: AI-assisted book description
The system SHALL allow librarians to generate a catalogue description and suggested genre tags for a book from its title, author, and optional ISBN via `POST /api/ai/describe`. The generated text SHALL be offered as a suggestion for the librarian to accept or edit, never written to the catalogue automatically.

#### Scenario: Description and genres are generated
- **WHEN** a librarian requests a description for a book by title and author
- **THEN** the response contains a short prose description and one or more suggested genre tags

#### Scenario: Suggestion is not auto-applied
- **WHEN** a description is generated for a book
- **THEN** the stored book record is unchanged until the librarian explicitly saves it

#### Scenario: Member cannot generate descriptions
- **WHEN** a user holding the `member` role calls `POST /api/ai/describe`
- **THEN** the response status is `403 Forbidden`

### Requirement: Personalised borrowing recommendations
The system SHALL provide personalised book recommendations at `GET /api/ai/recommend` to any authenticated user, derived from that user's recent borrowing history and the current catalogue. Recommendations SHALL favour books that are currently available, and SHALL each carry a short reason.

#### Scenario: Recommendations reflect borrowing history
- **WHEN** a user with prior loans requests recommendations
- **THEN** the response contains recommendations accompanied by reasons referring to their reading history

#### Scenario: Recommendations link back to catalogue books
- **WHEN** a recommendation corresponds to a book held in the catalogue
- **THEN** that recommendation carries the matching book's identifier so the user can navigate to it

#### Scenario: A user with no history still receives recommendations
- **WHEN** a user who has never borrowed a book requests recommendations
- **THEN** the response status is `200 OK` and general recommendations drawn from the catalogue are returned

### Requirement: AI responses are parsed defensively
The system SHALL treat language model output as untrusted, parsing it into the expected structure and degrading gracefully when parsing fails. A malformed model response SHALL NOT surface as a server error.

#### Scenario: Malformed search response falls back to keyword search
- **WHEN** the model returns output that cannot be parsed for a natural language search
- **THEN** the system falls back to searching on the user's raw query text rather than failing the request

#### Scenario: Malformed recommendation response returns an empty list
- **WHEN** the model returns output that cannot be parsed for recommendations
- **THEN** the response status is `200 OK` with an empty recommendation list

#### Scenario: Parse failures are logged
- **WHEN** any AI response fails to parse
- **THEN** a warning including the raw response is logged for diagnosis

### Requirement: AI provider is replaceable
The system SHALL access the language model through an OpenAI-compatible interface behind a service abstraction, so the provider or model can be changed through configuration and a single service implementation without altering endpoints or business logic.

#### Scenario: Endpoints depend on the abstraction
- **WHEN** the AI provider implementation is replaced with a different one satisfying the same interface
- **THEN** no endpoint or repository code requires modification

#### Scenario: API credentials come from configuration
- **WHEN** the application starts
- **THEN** the AI API key is read from configuration and is not present in source control

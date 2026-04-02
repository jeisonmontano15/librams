## ADDED Requirements

### Requirement: Backend test project exists
The system SHALL have an xUnit test project `LibraMS.Api.Tests` in `backend/LibraMS.Api.Tests/` that is part of the solution and runs with `dotnet test`.

#### Scenario: Test project builds and runs
- **WHEN** `dotnet test` is run from the `backend/` directory
- **THEN** the test runner discovers and executes all tests with no build errors

### Requirement: BookRepository unit tests
The system SHALL have unit tests covering the core query logic of `BookRepository` against a real PostgreSQL connection.

#### Scenario: SearchAsync with no filters returns all books paginated
- **WHEN** `SearchAsync` is called with a null query, null genre, null status, page 1, pageSize 20
- **THEN** it returns a `PagedResult` with `Items` and a correct `Total` count

#### Scenario: SearchAsync with text query filters results
- **WHEN** `SearchAsync` is called with a query string matching a known book title
- **THEN** only books matching that full-text query are returned

#### Scenario: SearchAsync with status filter returns only matching books
- **WHEN** `SearchAsync` is called with `status = BookStatus.Available`
- **THEN** all returned books have `status = "available"` in the database

#### Scenario: CreateAsync inserts and returns new book
- **WHEN** `CreateAsync` is called with a valid `CreateBookRequest`
- **THEN** a new row is inserted in `books` and the returned `Book` has a non-empty `Id`

#### Scenario: DeleteAsync removes the book
- **WHEN** `DeleteAsync` is called with the ID of an existing book
- **THEN** the method returns `true` and the book no longer exists in the database

### Requirement: LoanRepository unit tests
The system SHALL have unit tests covering checkout and checkin flows in `LoanRepository`.

#### Scenario: CheckOutAsync creates a loan for an available book
- **WHEN** `CheckOutAsync` is called for a book with `status = 'available'`
- **THEN** a new loan row is created with `status = 'active'` and the book status is set to `'checked_out'`

#### Scenario: CheckOutAsync returns null for an unavailable book
- **WHEN** `CheckOutAsync` is called for a book with `status = 'checked_out'`
- **THEN** the method returns `null` and no new loan row is created

#### Scenario: CheckInAsync returns the loan and frees the book
- **WHEN** `CheckInAsync` is called with an active loan ID
- **THEN** the loan `status` is set to `'returned'`, `returned_at` is populated, and the book status is set to `'available'`

### Requirement: API endpoint integration tests
The system SHALL have integration tests using `WebApplicationFactory<Program>` covering the happy path for each endpoint group.

#### Scenario: GET /api/books returns 200 with paged results
- **WHEN** an unauthenticated GET request is sent to `/api/books`
- **THEN** the response status is `200 OK` and the body is a valid `PagedResult<Book>`

#### Scenario: POST /api/books without auth returns 401
- **WHEN** an unauthenticated POST request is sent to `/api/books`
- **THEN** the response status is `401 Unauthorized`

#### Scenario: POST /api/books with librarian JWT returns 201
- **WHEN** a POST request with a valid librarian JWT is sent to `/api/books` with valid body
- **THEN** the response status is `201 Created` and the body contains the created book

#### Scenario: GET /health returns 200
- **WHEN** a GET request is sent to `/health`
- **THEN** the response status is `200 OK`

# loan-management Specification

## Purpose
Defines the borrowing lifecycle: checking books out and back in, the atomicity guarantees that prevent double-borrowing, members' access to their own loans and history, librarians' view of all loans, and overdue detection.
## Requirements
### Requirement: Members can check out an available book
The system SHALL allow any authenticated user to borrow an available book via `POST /api/loans/checkout/:bookId`, creating a loan record for the calling user and marking the book as checked out. The loan due date SHALL default to 14 days from checkout.

#### Scenario: Checking out an available book succeeds
- **WHEN** an authenticated user checks out a book whose status is `available`
- **THEN** the response status is `201 Created`, a loan is created for that user with status `active`, and the book's status becomes `checked_out`

#### Scenario: Loan due date defaults to 14 days
- **WHEN** a user checks out a book
- **THEN** the created loan's due date is 14 days after its checkout timestamp

#### Scenario: Checking out an already borrowed book is rejected
- **WHEN** an authenticated user attempts to check out a book whose status is `checked_out`
- **THEN** the response status is `409 Conflict`, no new loan is created, and the existing loan is unaffected

#### Scenario: Anonymous request cannot check out
- **WHEN** an unauthenticated client sends `POST /api/loans/checkout/:bookId`
- **THEN** the response status is `401 Unauthorized` and no loan is created

### Requirement: Checkout is atomic under concurrency
The system SHALL perform checkout inside a database transaction that locks the book row before reading its status, so that concurrent checkout attempts on the same book cannot both succeed. On any failure the transaction SHALL be rolled back, leaving neither a loan record nor a status change.

#### Scenario: Concurrent checkouts of the same book yield exactly one loan
- **WHEN** two users attempt to check out the same available book simultaneously
- **THEN** exactly one request returns `201 Created`, the other returns `409 Conflict`, and exactly one loan record exists for that book

#### Scenario: Failure during checkout leaves no partial state
- **WHEN** an error occurs after the loan insert but before the transaction commits
- **THEN** the transaction is rolled back, no loan record persists, and the book's status remains `available`

### Requirement: Books can be checked in
The system SHALL allow returning a borrowed book via `POST /api/loans/checkin/:loanId`, marking the loan as returned with a return timestamp and restoring the book's status to available. Check-in SHALL be atomic.

#### Scenario: Checking in an active loan succeeds
- **WHEN** a user checks in a loan whose status is `active`
- **THEN** the response status is `200 OK`, the loan status becomes `returned` with a return timestamp set, and the book's status becomes `available`

#### Scenario: Checking in an overdue loan succeeds
- **WHEN** a user checks in a loan whose status is `overdue`
- **THEN** the loan status becomes `returned` and the book becomes `available`

#### Scenario: Checking in an already returned loan returns 404
- **WHEN** a user checks in a loan whose status is already `returned`
- **THEN** the response status is `404 Not Found` and the book's status is unchanged

#### Scenario: Checking in an unknown loan returns 404
- **WHEN** a user sends `POST /api/loans/checkin/:loanId` for an ID that does not exist
- **THEN** the response status is `404 Not Found`

### Requirement: Members can view their own loans and history
The system SHALL expose `GET /api/loans/my` returning the calling user's outstanding loans ordered by due date, and `GET /api/loans/my/history` returning the calling user's past and present loans ordered most recent first. Both SHALL include the associated book details.

#### Scenario: Active loans list excludes returned loans
- **WHEN** an authenticated user requests `GET /api/loans/my`
- **THEN** the response contains only that user's loans whose status is not `returned`, each including its book details

#### Scenario: Active loans are ordered by due date
- **WHEN** a user with several outstanding loans requests `GET /api/loans/my`
- **THEN** the loans are ordered by due date ascending, soonest due first

#### Scenario: History includes returned loans
- **WHEN** an authenticated user requests `GET /api/loans/my/history`
- **THEN** the response includes that user's returned loans as well as outstanding ones, ordered by checkout date descending

#### Scenario: A user's loans are scoped to that user
- **WHEN** a user requests `GET /api/loans/my`
- **THEN** no loan belonging to another user appears in the response

### Requirement: Librarians can view all loans
The system SHALL expose `GET /api/loans/active` to users holding the `librarian` role, returning every outstanding loan across all users with associated book details, ordered by due date.

#### Scenario: Librarian sees loans across all users
- **WHEN** a librarian requests `GET /api/loans/active`
- **THEN** the response status is `200 OK` and includes outstanding loans belonging to users other than the caller

#### Scenario: Member cannot list all loans
- **WHEN** a user holding the `member` role requests `GET /api/loans/active`
- **THEN** the response status is `403 Forbidden`

### Requirement: Overdue loans are detected and reportable
The system SHALL mark any loan whose status is `active` and whose due date has passed as `overdue`. The system SHALL expose `GET /api/loans/overdue` to librarians, refreshing overdue status before returning the overdue loans ordered by due date.

#### Scenario: A loan past its due date becomes overdue
- **WHEN** overdue detection runs and a loan has status `active` with a due date in the past
- **THEN** that loan's status becomes `overdue`

#### Scenario: A loan within its due date is unaffected
- **WHEN** overdue detection runs and a loan has status `active` with a due date in the future
- **THEN** that loan's status remains `active`

#### Scenario: A returned loan is never marked overdue
- **WHEN** overdue detection runs and a loan has status `returned` with a due date in the past
- **THEN** that loan's status remains `returned`

#### Scenario: Overdue endpoint refreshes before returning
- **WHEN** a librarian requests `GET /api/loans/overdue` while a loan has just passed its due date
- **THEN** that newly overdue loan appears in the response without any separate refresh step

#### Scenario: Member cannot list overdue loans
- **WHEN** a user holding the `member` role requests `GET /api/loans/overdue`
- **THEN** the response status is `403 Forbidden`


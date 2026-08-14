## MODIFIED Requirements

### Requirement: Books can be checked in
The system SHALL allow returning a borrowed book via `POST /api/loans/checkin/:loanId`, marking the loan as returned with a return timestamp and restoring the book's status to available. Check-in SHALL be atomic. A member SHALL be able to check in only their own loans; users holding the `librarian` role SHALL be able to check in any loan, so that returns can be processed at the desk. A check-in request for a loan the caller may not return SHALL be answered identically to one for a loan that does not exist, so that loan identifiers belonging to other users are not disclosed.

#### Scenario: Member checks in their own active loan
- **WHEN** a member checks in a loan that belongs to them and whose status is `active`
- **THEN** the response status is `200 OK`, the loan status becomes `returned` with a return timestamp set, and the book's status becomes `available`

#### Scenario: Member cannot check in another member's loan
- **WHEN** a member sends `POST /api/loans/checkin/:loanId` for a loan belonging to a different user
- **THEN** the response status is `404 Not Found`, the loan remains outstanding, and the book's status is unchanged

#### Scenario: Librarian can check in any member's loan
- **WHEN** a user holding the `librarian` role checks in a loan belonging to a different user
- **THEN** the response status is `200 OK`, the loan status becomes `returned`, and the book's status becomes `available`

#### Scenario: Checking in an overdue loan succeeds
- **WHEN** a user checks in their own loan whose status is `overdue`
- **THEN** the loan status becomes `returned` and the book becomes `available`

#### Scenario: Checking in an already returned loan returns 404
- **WHEN** a user checks in their own loan whose status is already `returned`
- **THEN** the response status is `404 Not Found` and the book's status is unchanged

#### Scenario: Checking in an unknown loan returns 404
- **WHEN** a user sends `POST /api/loans/checkin/:loanId` for an ID that does not exist
- **THEN** the response status is `404 Not Found`

### Requirement: Members can check out an available book
The system SHALL allow any authenticated user to borrow an available book via `POST /api/loans/checkout/:bookId`, creating a loan record for the calling user and marking the book as checked out. The loan due date SHALL default to 14 days from checkout. The loan record SHALL retain the borrower's email address as it stood at the time of checkout, so that librarian views can identify the borrower without a further lookup. A checkout request naming a book that does not exist SHALL be distinguished from one naming a book that is already borrowed.

#### Scenario: Checking out an available book succeeds
- **WHEN** an authenticated user checks out a book whose status is `available`
- **THEN** the response status is `201 Created`, a loan is created for that user with status `active`, and the book's status becomes `checked_out`

#### Scenario: Borrower email is recorded on the loan
- **WHEN** an authenticated user checks out a book
- **THEN** the created loan record carries that user's email address

#### Scenario: Loan due date defaults to 14 days
- **WHEN** a user checks out a book
- **THEN** the created loan's due date is 14 days after its checkout timestamp

#### Scenario: Checking out an already borrowed book is rejected
- **WHEN** an authenticated user attempts to check out a book whose status is `checked_out`
- **THEN** the response status is `409 Conflict`, no new loan is created, and the existing loan is unaffected

#### Scenario: Checking out a nonexistent book returns 404
- **WHEN** an authenticated user attempts to check out a book ID that does not exist
- **THEN** the response status is `404 Not Found` and no loan is created

#### Scenario: Anonymous request cannot check out
- **WHEN** an unauthenticated client sends `POST /api/loans/checkout/:bookId`
- **THEN** the response status is `401 Unauthorized` and no loan is created

## ADDED Requirements

### Requirement: Loan storage matches the published schema
The system SHALL persist every field the checkout path writes in the schema published for provisioning, so that a database created from that schema supports checkout without further modification. Provisioning SHALL be idempotent, remaining safe to re-run against a database that already has the loan storage in place.

#### Scenario: Checkout succeeds on a freshly provisioned database
- **WHEN** a database is provisioned solely from the published schema and a user checks out an available book
- **THEN** the checkout succeeds and the loan is persisted with no missing-column error

#### Scenario: Re-running provisioning is a no-op
- **WHEN** the published schema is applied to a database that already contains the loan storage
- **THEN** the script completes successfully and no existing loan data is altered

#### Scenario: Loans predating the borrower email are still readable
- **WHEN** a loan row stored without a borrower email is read back
- **THEN** the loan is returned successfully with an empty borrower email rather than an error

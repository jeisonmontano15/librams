## ADDED Requirements

### Requirement: User repository exists and is registered
The system SHALL provide an `IUserRepository` interface and `UserRepository` implementation registered in the DI container, so the application starts without a runtime exception.

#### Scenario: Application starts with UserRepository registered
- **WHEN** the application starts
- **THEN** `IUserRepository` is resolvable from the DI container without error

#### Scenario: Get user by ID returns existing user
- **WHEN** `GetByIdAsync` is called with a valid user UUID that exists in `library_users`
- **THEN** the method returns the matching `LibraryUser` record

#### Scenario: Get user by ID returns null for unknown user
- **WHEN** `GetByIdAsync` is called with a UUID that does not exist in `library_users`
- **THEN** the method returns `null`

### Requirement: User upsert on first sign-in
The system SHALL provide `EnsureExistsAsync` that inserts a new user profile or does nothing if the user already exists, supporting OAuth sign-in flows.

#### Scenario: New user profile is created
- **WHEN** `EnsureExistsAsync` is called with an ID and email that do not exist in `library_users`
- **THEN** a new row is inserted with `role = 'member'`

#### Scenario: Existing user profile is not duplicated
- **WHEN** `EnsureExistsAsync` is called with an ID that already exists in `library_users`
- **THEN** no duplicate row is inserted and no error is thrown

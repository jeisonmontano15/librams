# authentication-authorization Specification

## Purpose
Defines how users prove identity via Google SSO, how API requests are authenticated by validated JWT, and how the member and librarian roles gate access at the API, the frontend routes, and the database.
## Requirements
### Requirement: Users sign in with Google SSO
The system SHALL authenticate users through Supabase Auth using Google as the single sign-on provider. The frontend SHALL redirect unauthenticated visitors to a login page and SHALL NOT collect or store passwords.

#### Scenario: Unauthenticated visitor is redirected to login
- **WHEN** an unauthenticated visitor navigates to any protected route
- **THEN** the application redirects them to the login page

#### Scenario: Successful sign-in establishes a session
- **WHEN** a user completes the Google sign-in flow
- **THEN** a session is established, the user's access token is available to the application, and they are admitted to the protected area

#### Scenario: Session is restored on reload
- **WHEN** a signed-in user reloads the application
- **THEN** the existing session is restored without requiring a second sign-in

### Requirement: API requests are authenticated with a validated JWT
The system SHALL require a bearer JWT issued by Supabase on all protected API endpoints. The API SHALL validate the token's signature against Supabase's published signing keys, and SHALL validate its issuer, audience, and expiry. Tokens failing any check SHALL be rejected.

#### Scenario: Valid token is accepted
- **WHEN** a client calls a protected endpoint with a valid unexpired Supabase JWT
- **THEN** the request is authenticated and processed

#### Scenario: Missing token is rejected
- **WHEN** a client calls a protected endpoint with no `Authorization` header
- **THEN** the response status is `401 Unauthorized`

#### Scenario: Expired token is rejected
- **WHEN** a client calls a protected endpoint with an expired JWT
- **THEN** the response status is `401 Unauthorized`

#### Scenario: Token with an invalid signature is rejected
- **WHEN** a client calls a protected endpoint with a JWT whose signature does not verify against the published signing keys
- **THEN** the response status is `401 Unauthorized`

#### Scenario: Token from an unexpected issuer is rejected
- **WHEN** a client calls a protected endpoint with a JWT whose issuer is not the configured Supabase project
- **THEN** the response status is `401 Unauthorized`

#### Scenario: Frontend attaches the token automatically
- **WHEN** the signed-in frontend issues any API request
- **THEN** the request carries the session access token as a bearer token without per-call handling

### Requirement: Users hold either the member or librarian role
The system SHALL assign every user a role of either `member` or `librarian`, defaulting to `member` on first sign-in. The role SHALL be stored in the database rather than in the identity token, and SHALL be constrained to those two values.

#### Scenario: First sign-in creates a member profile
- **WHEN** a user signs in for the first time
- **THEN** a user profile is created with role `member`

#### Scenario: Repeated sign-in does not duplicate the profile
- **WHEN** an existing user signs in again
- **THEN** their existing profile is reused and no duplicate is created

#### Scenario: Role is constrained to known values
- **WHEN** a write attempts to set a user's role to a value other than `member` or `librarian`
- **THEN** the database rejects the write

### Requirement: Roles are resolved from the database on each request
The system SHALL look up the authenticated user's role from the database and make it available to authorization policies on every request, so that a role change takes effect without reissuing the user's token.

#### Scenario: Role is available to authorization after authentication
- **WHEN** an authenticated request reaches an endpoint guarded by a role policy
- **THEN** the user's current stored role determines the authorization outcome

#### Scenario: Elevating a role takes effect on the next request
- **WHEN** a user's stored role is changed from `member` to `librarian`
- **THEN** their subsequent requests are authorized as a librarian without the token being reissued

### Requirement: Librarian-only operations are restricted
The system SHALL restrict book creation, editing, deletion, the all-loans and overdue listings, and AI description generation to users holding the `librarian` role. Members SHALL retain access to browsing, searching, borrowing, returning, their own loans, and recommendations.

#### Scenario: Member is denied a librarian-only endpoint
- **WHEN** a user holding the `member` role calls a librarian-only endpoint
- **THEN** the response status is `403 Forbidden`

#### Scenario: Librarian is granted a librarian-only endpoint
- **WHEN** a user holding the `librarian` role calls a librarian-only endpoint
- **THEN** the request is authorized and processed

#### Scenario: Member retains access to member capabilities
- **WHEN** a user holding the `member` role browses the catalogue, borrows a book, or views their own loans
- **THEN** each request is authorized and processed

### Requirement: Librarian-only navigation is hidden from members
The system SHALL guard librarian-only routes in the frontend, redirecting members away from them, in addition to enforcing the restriction at the API.

#### Scenario: Member navigating to a librarian route is redirected
- **WHEN** a user holding the `member` role navigates directly to a librarian-only route
- **THEN** they are redirected away and the librarian view is not rendered

#### Scenario: Librarian can reach librarian routes
- **WHEN** a user holding the `librarian` role navigates to a librarian-only route
- **THEN** the view is rendered

### Requirement: Access is enforced at the database level
The system SHALL enable row-level security on the users, books, and loans tables so that access rules hold independently of the API layer. Members SHALL be able to read the catalogue, read only their own loans, and read only their own profile; write access to books SHALL be limited to librarians.

#### Scenario: Member cannot read another user's loans directly
- **WHEN** a member queries the loans table directly for a loan belonging to another user
- **THEN** no rows are returned

#### Scenario: Member cannot write to the books table directly
- **WHEN** a member attempts to insert, update, or delete a row in the books table directly
- **THEN** the write is rejected

#### Scenario: Librarian can read all loans directly
- **WHEN** a librarian queries the loans table directly
- **THEN** loans belonging to all users are returned

#### Scenario: Member cannot read another user's profile
- **WHEN** a member queries the users table directly for another user's row
- **THEN** no rows are returned


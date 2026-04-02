## ADDED Requirements

### Requirement: Frontend test setup exists
The system SHALL have Vitest and React Testing Library configured so that `npm test` runs all tests in the `frontend/` directory.

#### Scenario: Test suite runs without configuration errors
- **WHEN** `npm test` is run from `frontend/`
- **THEN** Vitest discovers test files and runs them without setup errors

### Requirement: useAuth hook tests
The system SHALL have tests for the `useAuth` hook covering the authenticated and unauthenticated states.

#### Scenario: useAuth returns null user when not signed in
- **WHEN** `useAuth` is rendered inside an `AuthProvider` with no active Supabase session
- **THEN** `user` is `null` and `loading` is `false`

#### Scenario: useAuth returns user object when signed in
- **WHEN** `useAuth` is rendered inside an `AuthProvider` with a mock Supabase session
- **THEN** `user` is the mock user object and `loading` is `false`

### Requirement: LoginPage component test
The system SHALL have a test verifying the `LoginPage` renders a sign-in button.

#### Scenario: LoginPage renders Google sign-in button
- **WHEN** `LoginPage` is rendered
- **THEN** a button with accessible text matching "sign in" (case-insensitive) is visible in the DOM

### Requirement: BookFormModal component test
The system SHALL have tests for `BookFormModal` covering form validation feedback.

#### Scenario: BookFormModal submit with empty title shows validation error
- **WHEN** `BookFormModal` is rendered in create mode and the form is submitted without a title
- **THEN** a validation error message is visible in the DOM

#### Scenario: BookFormModal calls onSubmit with correct data
- **WHEN** `BookFormModal` is rendered, all required fields are filled, and the form is submitted
- **THEN** the `onSubmit` callback is called with the entered field values

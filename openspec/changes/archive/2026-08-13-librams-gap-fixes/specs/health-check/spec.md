## ADDED Requirements

### Requirement: Health check endpoint exists
The system SHALL expose a `GET /health` endpoint that returns `200 OK` with a JSON body, accessible without authentication, for use by Azure App Service probing and cold-start diagnostics.

#### Scenario: Health endpoint returns 200
- **WHEN** a GET request is sent to `/health`
- **THEN** the response status is `200 OK`

#### Scenario: Health endpoint is accessible without a JWT
- **WHEN** an unauthenticated GET request is sent to `/health`
- **THEN** the response status is `200 OK` and not `401 Unauthorized`

#### Scenario: Health endpoint returns a JSON status field
- **WHEN** a GET request is sent to `/health`
- **THEN** the response body is valid JSON containing a `status` field with value `"ok"`

## MODIFIED Requirements

### Requirement: AI endpoints are rate limited
The system SHALL apply a fixed-window rate limit of 10 requests per minute to all `/api/ai/*` endpoints using ASP.NET Core built-in rate limiting middleware. The limit SHALL be partitioned per caller — keyed on the authenticated user, falling back to the client's IP address when no authenticated identity is present — so that one caller's usage cannot consume another caller's quota.

#### Scenario: Request within limit is allowed
- **WHEN** a caller sends fewer than 10 requests to any `/api/ai/*` endpoint within a 60-second window
- **THEN** each request is processed normally and returns the expected response

#### Scenario: Request exceeding limit is rejected
- **WHEN** a caller sends an 11th request to any `/api/ai/*` endpoint within the same 60-second window
- **THEN** the response status is `429 Too Many Requests`

#### Scenario: One caller exhausting the limit does not affect another
- **WHEN** one authenticated user exhausts their 10-request window and a different authenticated user then calls an `/api/ai/*` endpoint
- **THEN** the second user's request is processed normally and does not receive `429`

#### Scenario: Rate limit resets after window expires
- **WHEN** a caller's 60-second window expires after hitting the limit
- **THEN** the next request from that caller to `/api/ai/*` is processed normally

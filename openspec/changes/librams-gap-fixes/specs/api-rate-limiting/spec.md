## ADDED Requirements

### Requirement: AI endpoints are rate limited
The system SHALL apply a fixed-window rate limit of 10 requests per minute per IP address to all `/api/ai/*` endpoints using ASP.NET Core built-in rate limiting middleware.

#### Scenario: Request within limit is allowed
- **WHEN** a client sends fewer than 10 requests to any `/api/ai/*` endpoint within a 60-second window
- **THEN** each request is processed normally and returns the expected response

#### Scenario: Request exceeding limit is rejected
- **WHEN** a client sends an 11th request to any `/api/ai/*` endpoint within the same 60-second window
- **THEN** the response status is `429 Too Many Requests`

#### Scenario: Rate limit resets after window expires
- **WHEN** a client's 60-second window expires after hitting the limit
- **THEN** the next request to `/api/ai/*` is processed normally

### Requirement: Non-AI endpoints are not rate limited
The system SHALL NOT apply rate limiting to `/api/books/*` or `/api/loans/*` endpoints.

#### Scenario: Book endpoints are unaffected by AI rate limit policy
- **WHEN** a client sends more than 10 requests to `/api/books` within 60 seconds
- **THEN** all requests are processed normally with no `429` responses

## MODIFIED Requirements

### Requirement: API requests are authenticated with a validated JWT
The system SHALL require a bearer JWT issued by Supabase on all protected API endpoints. The API SHALL validate the token's signature against Supabase's published signing keys, and SHALL validate its issuer, audience, and expiry. Tokens failing any check SHALL be rejected. The published signing keys SHALL be retrieved and cached rather than fetched on each validation, so that routine request handling performs no outbound network call, and the cache SHALL refresh periodically so that key rotation is picked up without a restart.

#### Scenario: Valid token is accepted
- **WHEN** a client calls a protected endpoint with a valid unexpired Supabase JWT
- **THEN** the request is authenticated and processed

#### Scenario: Signing keys are fetched once and reused
- **WHEN** several authenticated requests are validated in close succession
- **THEN** the signing keys are retrieved once and reused for the subsequent validations rather than fetched per request

#### Scenario: Rotated signing keys are picked up
- **WHEN** the published signing keys change and the cache refresh interval elapses
- **THEN** tokens signed with the new keys validate successfully without restarting the application

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

## ADDED Requirements

### Requirement: Browser origins are matched including wildcard subdomains
The system SHALL permit cross-origin requests from the configured frontend origin and from the deployment platform domains it declares. Where a declared origin uses a wildcard subdomain, the system SHALL match any subdomain of that domain rather than treating the pattern as a literal string.

#### Scenario: Configured frontend origin is permitted
- **WHEN** the browser sends a cross-origin request from the configured frontend URL
- **THEN** the request is permitted with credentials

#### Scenario: Wildcard subdomain origin is permitted
- **WHEN** the browser sends a cross-origin request from a subdomain matching a declared wildcard origin, such as a preview deployment
- **THEN** the request is permitted

#### Scenario: Unrelated origin is refused
- **WHEN** the browser sends a cross-origin request from an origin matching no configured or declared pattern
- **THEN** the request is not permitted

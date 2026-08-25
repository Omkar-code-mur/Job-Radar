# Research: Greenhouse Public Job Ingestion

## Decision: Use the Greenhouse Job Board API

**Decision**: Fetch public jobs from `GET /v1/boards/{board_token}/jobs?content=true`.

**Rationale**: Greenhouse documents this endpoint for public job-board data, it does not
require credentials for public boards, and it returns stable job identifiers, titles,
locations, departments, content, and application URLs. It supports a focused first adapter
without browser automation or protected-page access.

**Alternatives considered**:

- Greenhouse career-page HTML: rejected because it is less stable and unnecessary when the
  public board API is available.
- Lever API: deferred to a later adapter so the first slice has one source contract to test.
- Generic HTML scraping: deferred because selectors and source permission vary significantly.

## Decision: Store a board token in source configuration

**Decision**: A Greenhouse source requires a manually supplied `boardToken` in its configuration.
The configured source URL remains the public board URL for display, while the adapter builds the
API URL from the token.

**Rationale**: The token is the stable Greenhouse board identifier and avoids trying to infer
or crawl it from arbitrary pages. It is configuration, not a secret, but must still be validated
as non-empty and treated as untrusted input when constructing a request.

**Alternatives considered**:

- Infer the token from any career URL: rejected because arbitrary URLs are ambiguous.
- Store credentials: rejected because public Greenhouse job-board endpoints do not need them.

## Decision: Use bounded, respectful HTTP access

**Decision**: Use an `HttpClient` with a 10-second timeout, a truthful descriptive user-agent,
and at most two retries for transient failures such as timeouts, 408, 429, and 5xx responses.
Retries use increasing delay and must not retry malformed payloads or 4xx authorization failures.

**Rationale**: A small personal monitor does not need aggressive concurrency. Bounded retries
improve resilience while preventing indefinite traffic to a source.

**Alternatives considered**:

- Infinite retries: rejected because it can overload or repeatedly fail a source.
- Parallel requests per job: rejected because one board request returns the complete listing.

## Decision: Normalize defensively and isolate bad records

**Decision**: Require the Greenhouse job ID, title, and application URL. Map optional fields to
empty strings or `Unknown`, strip HTML from content for the normalized description, and skip an
invalid record with a diagnostic rather than failing the complete source scan.

**Rationale**: External payloads can change or contain incomplete records. A valid job list
should remain useful when one item is malformed.

**Alternatives considered**:

- Fail the entire scan on one invalid job: rejected because it violates source-failure isolation.
- Persist unvalidated payloads as jobs: rejected because the frontend and matcher require stable
  fields.

## Decision: Use source-scoped external identity

**Decision**: Deduplicate on `(companyId, sourceId, externalJobId)`. Existing records update
last-seen and mutable normalized fields; first-seen remains unchanged. Only an insert is a new
job for notification purposes.

**Rationale**: Greenhouse job IDs are stable within a board. Including company and source keeps
identifiers safe if the same external ID appears in another board.

**Alternatives considered**:

- URL-only identity: rejected because URLs can change.
- Title plus company: rejected because titles are not unique.

## Decision: Test at adapter, service, and contract boundaries

**Decision**: Test representative Greenhouse JSON fixtures, optional/malformed fields, HTTP
failure and retry behavior, normalization, duplicate upsert behavior, and the existing scan and
jobs endpoint response shapes.

**Rationale**: These tests cover the highest-risk boundaries without requiring live network calls.
A small fake `HttpMessageHandler` makes retry and payload cases deterministic.

**Alternatives considered**:

- Live-only integration tests: rejected because they are slow, rate-sensitive, and brittle.
- UI-only testing: rejected because source parsing and identity bugs would be hidden.

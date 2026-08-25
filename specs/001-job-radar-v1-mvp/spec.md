<!-- @format -->

# Feature Specification: Job Radar V1 MVP

**Feature Branch**: `001-job-radar-v1-mvp`

**Created**: 2026-08-25

**Status**: Draft

**Input**: User description: "Build Job Radar V1, a personal job-monitoring platform that watches manually configured company career pages and public job sources, normalizes and deduplicates jobs, evaluates them with transparent rule-based matching, and notifies the user about newly discovered strong matches."

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Monitor and Review New Jobs (Priority: P1)

As a job seeker, I want to configure companies and permitted public career sources so that Job Radar discovers new roles and presents them in one searchable workspace.

**Why this priority**: Reliable discovery is the foundation of every later workflow and provides value even before matching or notifications are enabled.

**Independent Test**: Configure one company and one supported source, run a scan, and verify that discovered roles appear with their company, source, title, location, application link, and posting information.

**Acceptance Scenarios**:

1. **Given** a configured and enabled public source, **When** the user starts a scan, **Then** successfully discovered roles are shown in the jobs workspace with normalized details.
2. **Given** multiple configured sources and one source failure, **When** the user scans all enabled sources, **Then** successful sources are processed and the failed source reports its problem without blocking the others.
3. **Given** a role was discovered during an earlier scan, **When** the same role is returned again, **Then** the existing role is updated or refreshed without creating a duplicate.
4. **Given** a role is shown in the jobs workspace, **When** the user searches or filters by supported job attributes, **Then** only matching roles are displayed and the user can open a detail view.

---

### User Story 2 - Configure Preferences and Understand Matches (Priority: P1)

As a job seeker, I want to maintain my preferred roles, skills, experience, locations, workplace preferences, and matching rules so that every discovered role receives an explainable relevance assessment.

**Why this priority**: Transparent relevance assessment is the product's central differentiator and prevents the user from manually reviewing every discovered listing.

**Independent Test**: Configure a candidate profile and matching rules, evaluate representative roles, and verify the score, criteria, missing skills, reasons, and threshold result against the configured values.

**Acceptance Scenarios**:

1. **Given** a candidate profile with preferred roles and skills, **When** a discovered role is evaluated, **Then** the result includes a score, scoring breakdown, matched criteria, missing criteria where detectable, and human-readable reasons.
2. **Given** configured scoring weights and a minimum threshold, **When** a role is evaluated, **Then** the result reflects those settings and identifies whether the role crosses the threshold.
3. **Given** a role contains an excluded keyword, **When** it is evaluated, **Then** the result identifies the exclusion and does not present the role as a qualifying match.
4. **Given** experience requirements cannot be detected, **When** a role is evaluated, **Then** the unknown experience requirement is not treated as an automatic mismatch.
5. **Given** a user changes profile or matching settings, **When** the next role is evaluated, **Then** the updated settings are used and remain available for later sessions.

---

### User Story 3 - Receive Reliable, Non-Repeating Alerts (Priority: P2)

As a job seeker, I want qualifying newly discovered roles to generate useful email alerts and appear in notification history without repeated alerts for the same role.

**Why this priority**: Timely alerts turn passive monitoring into an actionable workflow while notification history provides trust and auditability.

**Independent Test**: Discover a new qualifying role, verify one email notification and its history entry, then scan the same source again and verify no second successful notification is sent.

**Acceptance Scenarios**:

1. **Given** a newly discovered role crosses the configured threshold, **When** processing completes, **Then** the user receives an alert containing the role, company, location, score, score explanation, relevant skills, missing skills where detectable, posting date, and application link.
2. **Given** a role was already successfully notified for the profile, **When** it is encountered again, **Then** no additional successful alert is sent unless repeat alerts are explicitly enabled.
3. **Given** alert delivery fails, **When** the failure is recorded, **Then** the user can see the failure in notification history and a bounded retry can be attempted without creating duplicate successful notifications.
4. **Given** scheduled monitoring is enabled, **When** the hourly monitoring window occurs, **Then** the same scan workflow runs without requiring the user to keep the workspace open.

### Edge Cases

- A source returns malformed data, missing required fields, an empty result, an HTTP failure, a timeout, or a rate-limit response.
- Two source records refer to the same external role, or a role changes title, description, location, or posting date between scans.
- A source becomes unavailable after previously succeeding, is disabled by the user, or is not supported by the permitted source types.
- A job has no posted date, no detectable experience requirement, no location, or an application link that differs from its source page.
- A candidate profile has no preferred roles or skills, has overlapping include and exclude keywords, or uses weights that do not total 100 percent.
- A role matches the score threshold but has already been successfully notified for the same profile.
- An alert provider is unavailable while job persistence and matching succeed.
- Seeded demonstration data is present alongside live data and must remain clearly identified.

## Requirements _(mandatory)_

### Functional Requirements

- **FR-001**: The system MUST allow the user to create, update, enable, disable, and remove monitored companies.
- **FR-002**: The system MUST allow each company to have one or more named career sources with a source type, public location, enabled state, configuration, and health information.
- **FR-003**: The system MUST support public Greenhouse and Lever job feeds plus supported structured career pages, and MUST report unsupported sources without attempting to bypass access controls.
- **FR-004**: The system MUST fetch jobs from enabled sources through a common monitoring workflow and MUST isolate failures so one source cannot stop other source scans.
- **FR-005**: The system MUST convert source results into a consistent job record containing, at minimum, company, source, external identifier, title, description, location, workplace type, department, employment type, posting date, application link, source link, first-seen time, last-seen time, and retained source metadata.
- **FR-006**: The system MUST validate normalized job records and handle missing or malformed fields without crashing the complete scan.
- **FR-007**: The system MUST identify an existing job by company, source, and external identifier, and MUST make repeated scans idempotent.
- **FR-008**: The system MUST provide job search, sorting, pagination, and filtering by company, source, location, workplace type, posting date, match status, notification status, and minimum match score.
- **FR-009**: The system MUST provide a job detail view with normalized job information, source information, matching information, notification status, and a direct application link.
- **FR-010**: The system MUST allow the user to maintain preferred roles, skills, technologies, minimum and maximum experience, preferred locations, workplace preference, include keywords, exclude keywords, and minimum match score.
- **FR-011**: The system MUST allow the user to configure matching weights for role relevance, skills, experience, location, explicit AI/GenAI keyword relevance, and freshness, with weights validated to total 100 percent.
- **FR-012**: The system MUST evaluate jobs using deterministic rules only in V1 and MUST NOT use LLMs, embeddings, semantic AI, or AI agents.
- **FR-013**: The matching result MUST include a score from 0 to 100, threshold result, score breakdown, matched criteria, missing criteria where detectable, and explanations tied to the evaluated job content.
- **FR-014**: The matching rules MUST compare role/title terms, configured skills and technologies, common experience expressions, locations and workplace preferences, explicit AI/GenAI terms, freshness, include keywords, and exclude keywords.
- **FR-015**: The system MUST treat an undetectable experience requirement as unknown rather than as an automatic mismatch.
- **FR-016**: The system MUST identify newly discovered jobs separately from previously known jobs and MUST only initiate default notifications for newly discovered qualifying jobs.
- **FR-017**: The system MUST provide an email notification service that includes the job title, company, location, workplace type, detected experience requirement, score, score breakdown, matched criteria, missing skills where detectable, posting date, and application link.
- **FR-018**: The system MUST record notification type, job, profile, status, sent time, failure information, and creation time, and MUST prevent a second successful notification for the same job and profile by default.
- **FR-019**: The system MUST provide manual scan actions for one source and all enabled sources.
- **FR-020**: The system MUST provide an approximately hourly monitoring schedule that triggers the same application workflow as a manual scan.
- **FR-021**: The system MUST track each source's last attempt, last success, status, failure count, last error, fetched job count, and useful fetch timing.
- **FR-022**: The system MUST use bounded retries with backoff, request timeouts, respectful request frequency, and a truthful user-agent for permitted source access.
- **FR-023**: The system MUST provide consistent errors and useful logs for fetch, normalization, persistence, matching, scheduling, and notification failures.
- **FR-024**: The system MUST persist companies, sources, jobs, profiles, matching settings, match results, notifications, and fetch history across application restarts.
- **FR-025**: The system MUST provide development seed data for companies, sources, jobs, profiles, matches, and notifications, and MUST clearly label it as demonstration data.
- **FR-026**: The system MUST expose documented service behavior for dashboard metrics, companies, sources, jobs, profiles, matching, notifications, and scan operations while preserving existing client behavior where applicable.
- **FR-027**: The system MUST keep credentials and operational secrets outside source code and MUST provide documented configuration expectations for local use.
- **FR-028**: The system MUST include focused automated tests for matching rules, normalization, deduplication, notification suppression, source failures, and representative source responses.

### Key Entities

- **Company**: A monitored organization with identity and career information.
- **Career Source**: A permitted public source belonging to a company, including type, location, configuration, enabled state, and health status.
- **Job**: A normalized role discovered from a career source, including its stable external identity, descriptive fields, timestamps, and source metadata.
- **Candidate Profile**: The user's career preferences, skills, experience range, locations, workplace preference, and keyword preferences.
- **Matching Configuration**: The user's scoring weights, threshold, and matching options.
- **Job Match**: The explainable evaluation of a job against a candidate profile, including score, breakdown, matched criteria, missing criteria, reasons, and evaluation time.
- **Notification**: The history and outcome of an alert attempt for a job and candidate profile.
- **Fetch Log**: The result of a source access attempt, including timing, count, status, and failure information.

## Success Criteria _(mandatory)_

### Measurable Outcomes

- **SC-001**: A user can configure one company and one permitted source and complete the first manual scan in under 5 minutes.
- **SC-002**: At least 99 percent of successfully returned valid job records are visible with the required normalized fields after a completed source scan.
- **SC-003**: Repeating the same scan 10 times creates zero duplicate job records and zero additional successful notifications for already-notified jobs.
- **SC-004**: At least 95 percent of job searches and filters return the first result set within 2 seconds for a development dataset of 10,000 stored jobs.
- **SC-005**: A user can determine why a qualifying or non-qualifying job received its result using the displayed breakdown, matched criteria, missing criteria, and reasons in under 60 seconds.
- **SC-006**: In a test run containing one failed source and at least two successful sources, 100 percent of successful sources complete processing and the failed source reports an actionable status.
- **SC-007**: For newly discovered jobs that cross the configured threshold, at least 95 percent of successful notification attempts contain all required job, score, explanation, and application-link fields.
- **SC-008**: A user can complete the primary workflow of configuring preferences, scanning sources, reviewing a match, and opening its application link without encountering an unexplained error.
- **SC-009**: The hourly monitoring workflow completes its scheduled attempt within the configured monitoring window and leaves a visible source health and scan result.
- **SC-010**: No V1 user workflow invokes semantic AI services, and the product remains usable when all AI-related future integrations are absent.

## Assumptions

- The initial release serves one personal user profile; multi-user authorization is outside this feature's scope unless separately specified.
- Companies and sources are manually configured initially; bulk company import is a future enhancement.
- Greenhouse, Lever, and explicitly supported structured pages are the initial permitted source categories; generic or protected pages may remain unsupported.
- A scan is considered successful per source when the source response is usable, even if it returns zero jobs.
- Default freshness scoring uses the posting date when available and does not reject a job when the date is unknown.
- Notification retries are bounded and controlled; permanent delivery failure does not block persistence, matching, or later source scans.
- Existing dashboard concepts, routes, and seeded development experience remain available while persistent storage and live monitoring are introduced.
- Initial operation prioritizes local or free/low-cost deployment and does not require a specific cloud provider.
- The user is responsible for configuring permitted sources and notification credentials and for complying with source terms of use.
- Future semantic matching may be added behind the matching boundary, but it is explicitly excluded from V1 acceptance.

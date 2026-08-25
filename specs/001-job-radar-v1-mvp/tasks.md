---

description: "Task list for Greenhouse public job ingestion"
---

# Tasks: Greenhouse Public Job Ingestion

**Input**: Design documents from `/specs/001-job-radar-v1-mvp/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/greenhouse-source.md,
quickstart.md

**Organization**: Tasks are grouped by user story so each story can be implemented and tested
as an independent increment.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the .NET backend and repository for source ingestion without changing the
frontend contract.

- [X] T001 Add the ASP.NET Core project to the documented local development workflow in `artifacts/api-server-dotnet/JobRadar.Api.csproj` and `README.md`
- [X] T002 [P] Add Greenhouse source configuration examples, including `boardToken`, to `artifacts/api-server-dotnet/appsettings.Development.json` and `.env.example`
- [X] T003 [P] Add .NET test project configuration at `artifacts/api-server-dotnet.tests/JobRadar.Api.Tests.csproj` with xUnit and a project reference to the API
- [X] T004 [P] Add representative Greenhouse response fixture at `artifacts/api-server-dotnet.tests/Fixtures/greenhouse-jobs.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish source-independent models, repository behavior, and shared HTTP/error
handling required by all user stories.

- [X] T005 Create source-independent job, source configuration, and scan result models in `artifacts/api-server-dotnet/Program.cs` matching `contracts/greenhouse-source.md`
- [ ] T006 Create a source adapter contract and fetch result type in `artifacts/api-server-dotnet/Sources/IJobSource.cs`
- [ ] T007 Create an in-memory job/source repository abstraction with lookup by `(companyId, sourceId, externalJobId)` in `artifacts/api-server-dotnet/Repositories/IJobRepository.cs` and `artifacts/api-server-dotnet/Repositories/InMemoryJobRepository.cs`
- [X] T008 Add a bounded HTTP client configuration with a 10-second timeout, truthful user-agent, transient-status classification, and maximum two retries in `artifacts/api-server-dotnet/Program.cs` and `artifacts/api-server-dotnet/Sources/Greenhouse/GreenhouseJobSource.cs`
- [ ] T009 [P] Add unit tests for repository insert/update identity and first-seen/last-seen behavior in `artifacts/api-server-dotnet.tests/Repositories/InMemoryJobRepositoryTests.cs`
- [ ] T010 [P] Add HTTP retry and timeout tests using a fake `HttpMessageHandler` in `artifacts/api-server-dotnet.tests/Sources/Greenhouse/GreenhouseHttpClientTests.cs`
- [X] T011 Register the HTTP client and Greenhouse source service in `artifacts/api-server-dotnet/Program.cs`

**Checkpoint**: The API can construct source-independent services, and repository/retry tests
pass before any endpoint is connected.

---

## Phase 3: User Story 1 - Monitor and Review New Jobs (Priority: P1)

**Goal**: Configure a public Greenhouse source, scan it, normalize valid jobs, deduplicate
repeated observations, and expose the existing jobs/dashboard views.

**Independent Test**: Configure a Greenhouse board fixture, run a source scan, verify normalized
jobs and source health, then run the same scan again and verify no duplicates are created.

### Tests for User Story 1

- [X] T012 [P] [US1] Test mapping of Greenhouse IDs, titles, content, location, departments, updated date, and application URLs in `artifacts/api-server-dotnet.tests/Sources/Greenhouse/GreenhouseNormalizerTests.cs`
- [X] T013 [P] [US1] Test skipping malformed Greenhouse items while retaining valid items and diagnostics in `artifacts/api-server-dotnet.tests/Sources/Greenhouse/GreenhouseNormalizerTests.cs`
- [X] T014 [P] [US1] Verify Greenhouse board token derivation from a validated board URL through the source creation API smoke test in `artifacts/api-server-dotnet/Program.cs`
- [ ] T015 [P] [US1] Add API contract tests for `POST /api/sources/{id}/scan`, `GET /api/jobs`, and `GET /api/dashboard` in `artifacts/api-server-dotnet.tests/Api/GreenhouseIngestionEndpointTests.cs`

### Implementation for User Story 1

- [X] T016 [US1] Implement Greenhouse response DTOs and JSON deserialization in `artifacts/api-server-dotnet/Sources/Greenhouse/GreenhouseDtos.cs`
- [X] T017 [US1] Implement defensive HTML-to-text description normalization and workplace/location/department mapping in `artifacts/api-server-dotnet/Sources/Greenhouse/GreenhouseNormalizer.cs`
- [X] T018 [US1] Implement `GreenhouseJobSource` using the public board endpoint and `boardToken` validation in `artifacts/api-server-dotnet/Sources/Greenhouse/GreenhouseJobSource.cs`
- [X] T019 [US1] Implement source scan orchestration that fetches, normalizes, upserts, counts new jobs, and updates source health in `artifacts/api-server-dotnet/Program.cs`
- [X] T020 [US1] Add configured source creation/update support for Greenhouse `boardToken` without accepting credentials in `artifacts/api-server-dotnet/Program.cs`
- [X] T021 [US1] Update `POST /api/sources/{id}/scan` and `POST /api/scheduler/scan` endpoints to invoke ingestion while isolating per-source failures in `artifacts/api-server-dotnet/Program.cs`
- [X] T022 [US1] Update jobs, dashboard, and source health responses to return live in-memory ingestion results while preserving generated client shapes in `artifacts/api-server-dotnet/Program.cs`
- [X] T023 [US1] Verify through the live Greenhouse scan smoke test that a repeated scan creates zero duplicate jobs in `artifacts/api-server-dotnet/Program.cs`
- [X] T024 [US1] Document how to add a Greenhouse source and board token in `specs/001-job-radar-v1-mvp/quickstart.md` and `README.md`

**Checkpoint**: US1 is independently usable when a public Greenhouse board token is configured;
normalized roles appear in the existing React jobs/dashboard views.

---

## Phase 4: User Story 2 - Configure Preferences and Understand Matches (Priority: P1)

**Goal**: Evaluate newly ingested Greenhouse jobs with deterministic, explainable matching and
persist the result in the current development store.

**Independent Test**: Scan fixture jobs with a configured profile and verify role, skill,
location, AI-keyword, freshness, exclusion, score, threshold, and missing-criteria behavior.

### Tests for User Story 2

- [ ] T025 [P] [US2] Add rule-based matcher tests for title and skill matches, missing skills, and explicit AI keywords in `artifacts/api-server-dotnet.tests/Matching/RuleBasedMatcherTests.cs`
- [ ] T026 [P] [US2] Add matcher tests for location/workplace preference, unknown experience, excluded keywords, freshness, weights, and threshold in `artifacts/api-server-dotnet.tests/Matching/RuleBasedMatcherTests.cs`
- [ ] T027 [P] [US2] Add tests proving profile and matching configuration updates validate and survive subsequent evaluations in `artifacts/api-server-dotnet.tests/Api/ProfileAndMatchingEndpointTests.cs`

### Implementation for User Story 2

- [ ] T028 [US2] Implement the `IMatchingEngine` contract and deterministic score breakdown in `artifacts/api-server-dotnet/Matching/IMatchingEngine.cs` and `artifacts/api-server-dotnet/Matching/RuleBasedMatcher.cs`
- [ ] T029 [US2] Implement profile and matching configuration validation, including non-negative fields and weights totaling 100, in `artifacts/api-server-dotnet/Validation/MatchingValidation.cs`
- [ ] T030 [US2] Connect ingestion results to the matching service and store score, match status, breakdown, matched criteria, missing criteria, and reasons in `artifacts/api-server-dotnet/Services/MatchingService.cs`
- [ ] T031 [US2] Update profile and matching endpoints to use validated service state in `artifacts/api-server-dotnet/Program.cs`
- [ ] T032 [US2] Add job detail response fields for match explanations and notification status in `artifacts/api-server-dotnet/Program.cs`

**Checkpoint**: A scanned Greenhouse job displays a deterministic score and understandable
matching explanation in the existing UI.

---

## Phase 5: User Story 3 - Receive Reliable, Non-Repeating Alerts (Priority: P2)

**Goal**: Notify the user about newly discovered qualifying jobs and suppress duplicate alerts.

**Independent Test**: Process a new qualifying fixture job, verify one notification record, then
process it again and verify no second successful notification is recorded.

### Tests for User Story 3

- [ ] T033 [P] [US3] Add notification eligibility and duplicate-suppression tests keyed by job and profile in `artifacts/api-server-dotnet.tests/Notifications/NotificationServiceTests.cs`
- [ ] T034 [P] [US3] Add notification failure and bounded retry tests in `artifacts/api-server-dotnet.tests/Notifications/NotificationServiceTests.cs`
- [ ] T035 [P] [US3] Add API tests for notification history response fields and status handling in `artifacts/api-server-dotnet.tests/Api/NotificationEndpointTests.cs`

### Implementation for User Story 3

- [ ] T036 [US3] Add notification records and a notification repository abstraction in `artifacts/api-server-dotnet/Notifications/NotificationModels.cs` and `artifacts/api-server-dotnet/Repositories/INotificationRepository.cs`
- [ ] T037 [US3] Add `INotificationService` and a development email sender abstraction in `artifacts/api-server-dotnet/Notifications/INotificationService.cs` and `artifacts/api-server-dotnet/Notifications/EmailNotificationService.cs`
- [ ] T038 [US3] Connect newly inserted qualifying jobs to notification eligibility and history recording in `artifacts/api-server-dotnet/Services/JobMonitoringService.cs`
- [ ] T039 [US3] Expose notification history and delivery status through `GET /api/notifications` in `artifacts/api-server-dotnet/Program.cs`
- [ ] T040 [US3] Add local SMTP configuration documentation and ensure credentials are read only from environment configuration in `artifacts/api-server-dotnet/appsettings.example.json` and `README.md`

**Checkpoint**: Newly discovered qualifying jobs can produce one recorded alert per profile, and
failed delivery does not block persistence or future scans.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T041 [P] Add structured source-fetch, normalization, matching, and notification logs in `artifacts/api-server-dotnet/Logging/JobRadarLogging.cs`
- [ ] T042 [P] Add source health diagnostics for partial malformed records, retry recovery, and exhausted retries in `artifacts/api-server-dotnet/Services/SourceHealthService.cs`
- [ ] T043 [P] Add API documentation updates for Greenhouse configuration and scan behavior in `lib/api-spec/openapi.yaml`
- [ ] T044 Regenerate the TypeScript API client and Zod schemas with `npm run codegen --workspace=@workspace/api-spec` after contract changes
- [ ] T045 [P] Add `.NET` and npm build/test commands to `README.md` and `specs/001-job-radar-v1-mvp/quickstart.md`
- [ ] T046 Run the full backend test suite and frontend typecheck/build, recording results in `specs/001-job-radar-v1-mvp/quickstart.md`
- [ ] T047 [P] Add PostgreSQL repository interfaces and migration notes without changing the Greenhouse adapter contract in `artifacts/api-server-dotnet/Repositories/` and `specs/001-job-radar-v1-mvp/data-model.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 has no prerequisites and establishes project/test setup.
- Phase 2 depends on Phase 1 and blocks all user stories.
- US1 depends on Phase 2 and is the recommended MVP increment.
- US2 depends on US1's normalized jobs and scan orchestration.
- US3 depends on US1's new-job identity and US2's match result.
- Phase 6 depends on the completed story phases, except documentation and logging tasks that
  may run in parallel after their referenced code exists.

### User Story Completion Order

```text
Phase 1 -> Phase 2 -> US1 -> US2 -> US3 -> Phase 6
```

### Parallel Opportunities

- T002, T003, and T004 can run in parallel during setup.
- T009 and T010 can run in parallel after the foundational abstractions are defined.
- T012-T015 can run in parallel because they use separate test concerns/files.
- T025-T027 can run in parallel because they cover separate matching/configuration concerns.
- T033-T035 can run in parallel because they cover separate notification concerns.
- T041-T043 and T045 can run in parallel after the relevant runtime behavior is implemented.

## Implementation Strategy

1. **MVP first**: Complete Phase 1, Phase 2, and US1 only. This proves public Greenhouse fetch,
   normalization, idempotent in-memory storage, source health, and React visibility.
2. **Incremental delivery**: Add US2 to make live jobs explainably match the candidate profile,
   then US3 for non-repeating alerts.
3. **Preserve boundaries**: Keep `IJobSource`, `IMatchingEngine`, repository abstractions, and
   `INotificationService` independent of ASP.NET endpoint details.
4. **Defer persistence migration**: Implement PostgreSQL behind the repository boundary after
   the live-source vertical slice is stable.
5. **Validate continuously**: Run focused tests after each story, then the full backend and
   frontend checks in Phase 6.

## Format Validation

All implementation tasks use the required format: `- [ ] T### [P?] [US#?] description with an
exact file path`. Setup, foundational, and polish tasks omit story labels; user-story tasks
include `[US1]`, `[US2]`, or `[US3]`. Parallel markers appear only on tasks intended for separate
files or independent test concerns.

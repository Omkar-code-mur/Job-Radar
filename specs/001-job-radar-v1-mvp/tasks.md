---
description: "Task list for clarified Greenhouse public job ingestion"
---

<!-- @format -->

# Tasks: Greenhouse Public Job Ingestion

**Input**: Design documents from `/specs/001-job-radar-v1-mvp/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/greenhouse-source.md, quickstart.md

**Scope**: This task list covers Greenhouse fetching, normalization, composite-identity deduplication, `lastSeenAt`, source health, and aggregate failure isolation. Matching, notifications, email, and scheduled monitoring are outside this feature. Existing company CRUD, authentication, PostgreSQL persistence, basic job search, and registered source adapters must not be reimplemented as duplicate tasks.

**Organization**: Tasks are grouped by the single in-scope user story and ordered by dependency.

## Phase 1: Setup

**Purpose**: Establish focused verification coverage without changing unrelated product behavior.

- [x] T001 [P] Add Greenhouse ingestion test folders and shared fixture-loading helpers in `artifacts/api-server-dotnet.tests/Sources/Greenhouse/GreenhouseTestFixture.cs`
- [x] T002 [P] Add a representative mixed-validity Greenhouse response fixture covering duplicate observations, missing optional fields, and malformed records in `artifacts/api-server-dotnet.tests/Fixtures/greenhouse-ingestion-edge-cases.json`
- [x] T003 Update the Greenhouse validation commands and expected composite-identity, `lastSeenAt`, and source-health checks in `specs/001-job-radar-v1-mvp/quickstart.md`

## Phase 2: Foundational

**Purpose**: Establish the normalized identity, persistence, and aggregate scan behavior required by US1.

- [x] T004 Add `ExternalJobId` and `LastSeenAt` to the normalized job contract and ensure the scan result can carry per-source failures in `artifacts/api-server-dotnet/Program.cs`
- [x] T005 Update PostgreSQL job schema, read/write mapping, and indexes so `(CompanyId, SourceId, ExternalJobId)` is unique while `FirstSeenAt` is preserved and `LastSeenAt` is updated in `artifacts/api-server-dotnet/Database/PostgresJobRadarStore.cs`
- [x] T006 Update the OpenAPI job and scan-result schemas for `externalJobId`, `lastSeenAt`, and aggregate per-source failure details in `lib/api-spec/openapi.yaml`
- [x] T007 Regenerate the derived React Query client and Zod schemas from the OpenAPI contract with `npm run codegen --workspace=@workspace/api-spec` in `lib/api-client-react/src/generated/` and `lib/api-zod/src/generated/`
- [x] T008 Add source-health persistence logic that increments consecutive failures, records the latest error, and resets `failureCount` to zero after a successful fetch in `artifacts/api-server-dotnet/Database/PostgresJobRadarStore.cs`
- [x] T009 Change multi-source scan orchestration to catch an individual source failure, update that source health, append per-source failure details, and continue remaining sources in `artifacts/api-server-dotnet/Database/PostgresJobRadarStore.cs`

**Checkpoint**: The API has a stable composite job identity, explicit first/last observation timestamps, truthful consecutive source health, and an aggregate scan result that can represent partial failure.

## Phase 3: User Story 1 - Monitor and Review New Greenhouse Jobs (Priority: P1)

**Goal**: Fetch permitted public Greenhouse jobs, normalize valid records, skip malformed records, deduplicate repeated observations, update source health, and expose the result through the existing dashboard workflow.

**Independent Test**: Configure a Greenhouse source backed by the fixture, scan it with one malformed record, scan the same records again, and verify valid jobs are persisted once, `firstSeenAt` is stable, `lastSeenAt` advances, malformed records are diagnosed, and a failing source does not prevent another source from completing.

### Tests for User Story 1

- [x] T010 [P] [US1] Extend Greenhouse normalizer tests for required-field validation, optional-field defaults, HTML-to-text conversion, external ID mapping, and diagnostic output in `artifacts/api-server-dotnet.tests/Sources/Greenhouse/GreenhouseNormalizerTests.cs`
- [ ] T011 [P] [US1] Add PostgreSQL-backed upsert tests proving composite identity deduplication, stable `firstSeenAt`, updated `lastSeenAt`, and mutable-field refresh in `artifacts/api-server-dotnet.tests/Database/PostgresJobRadarStoreIngestionTests.cs`
- [ ] T012 [P] [US1] Add source-health tests proving consecutive failure increments, latest error recording, successful reset, and zero-job successful fetch behavior in `artifacts/api-server-dotnet.tests/Database/SourceHealthTests.cs`
- [ ] T013 [P] [US1] Add aggregate scan tests proving one failing source does not abort successful sources and per-source failure details are returned in `artifacts/api-server-dotnet.tests/Api/GreenhouseIngestionEndpointTests.cs`
- [x] T014 [P] [US1] Add HTTP behavior tests for Greenhouse timeout, transient status retries, bounded retry exhaustion, truthful user-agent, and malformed JSON in `artifacts/api-server-dotnet.tests/Sources/Greenhouse/GreenhouseHttpClientTests.cs`
- [ ] T015 [P] [US1] Add API contract tests for source scan, jobs, dashboard, and source-health responses with the new fields in `artifacts/api-server-dotnet.tests/Api/GreenhouseIngestionEndpointTests.cs`

### Implementation for User Story 1

- [x] T016 [US1] Update Greenhouse DTO mapping and normalization to retain the source external job ID, normalized metadata, and observation timestamps in `artifacts/api-server-dotnet/Sources/Greenhouse/GreenhouseDtos.cs` and `artifacts/api-server-dotnet/Sources/Greenhouse/GreenhouseNormalizer.cs`
- [x] T017 [US1] Update Greenhouse fetch diagnostics so malformed individual records are skipped with source-level diagnostic information while valid records continue in `artifacts/api-server-dotnet/Sources/Greenhouse/GreenhouseJobSource.cs`
- [x] T018 [US1] Update source creation and validation to accept only the public Greenhouse board URL/token configuration required by this slice and reject credential-like configuration in `artifacts/api-server-dotnet/Database/PostgresJobRadarStore.cs`
- [x] T019 [US1] Connect the updated scan result and source-health fields to the existing single-source and scan-all endpoints without changing unrelated API behavior in `artifacts/api-server-dotnet/Program.cs`
- [x] T020 [US1] Update the jobs, dashboard, and source-health frontend views to display the new ingestion fields and aggregate scan failures without adding matching or notification behavior in `artifacts/job-radar/src/App.tsx`
- [x] T021 [US1] Update the Greenhouse contract and data-model documentation with the final response fields, composite identity, last-seen semantics, and consecutive failure reset in `specs/001-job-radar-v1-mvp/contracts/greenhouse-source.md` and `specs/001-job-radar-v1-mvp/data-model.md`

**Checkpoint**: US1 is independently usable when a permitted Greenhouse board is configured; valid roles appear once in the existing jobs workspace, repeated observations refresh `lastSeenAt`, source health is truthful, and partial scans preserve successful results.

## Phase 4: Polish and Cross-Cutting Verification

- [x] T022 [P] Add structured ingestion logs for fetch start/end, malformed-record counts, upsert counts, source failures, and aggregate scan completion in `artifacts/api-server-dotnet/Program.cs` and `artifacts/api-server-dotnet/Sources/Greenhouse/GreenhouseJobSource.cs`
- [x] T023 [P] Document that matching, notifications, email delivery, and scheduling are outside this feature and link the repository reconciliation in `specs/001-job-radar-v1-mvp/plan.md` and `specs/001-job-radar-v1-mvp/reconciliation.md`
- [x] T024 Run the focused backend ingestion tests, shared library typecheck, frontend typecheck/build, and backend build; record the results in `specs/001-job-radar-v1-mvp/quickstart.md`

## Dependencies and Execution Order

```text
Phase 1 -> Phase 2 -> US1 tests -> US1 implementation -> Phase 4 verification
```

- Phase 1 has no prerequisites.
- Phase 2 blocks US1 because identity, persistence, API shape, and aggregate scan behavior must be defined first.
- US1 test tasks can be written in parallel after the fixture and contract shape are agreed.
- US1 implementation tasks depend on the foundational model and persistence tasks.
- Phase 4 follows US1 implementation; documentation-only tasks may run in parallel with verification.

## Parallel Opportunities

- T001 and T002 can run in parallel.
- T006 and T008 can run in parallel after T004 defines the result and source-health fields.
- T010-T015 can run in parallel because they target separate test concerns/files.
- T016-T018 can run in parallel after T004/T005 establish the normalized model and persistence shape.
- T021-T023 can run in parallel after the runtime behavior is settled.

## Implementation Strategy

1. **MVP first**: Complete Phase 1, Phase 2, and US1. This is the entire current feature scope.
2. **Preserve existing behavior**: Reuse the active PostgreSQL store, source factory, auth boundary, and generated API workflow.
3. **Verify before expanding**: Run the focused ingestion suite and repository checks before planning matching, notifications, email, or scheduling as separate features.
4. **Do not duplicate existing work**: Company CRUD, authentication, PostgreSQL initialization, basic jobs UI, and existing Greenhouse/Deloitte registration are repository capabilities, not new tasks here.

## Format Validation

All implementation tasks use `- [ ] T### [P?] [US#?] description with an exact file path`. Setup, foundational, and polish tasks omit story labels; US1 tasks include `[US1]`. Parallel markers are used only for independent files or test concerns.

## Explicitly Out of Scope

- Deterministic matcher redesign or `IMatchingEngine` extraction.
- Notification eligibility, suppression, email delivery, and retry orchestration.
- Hosted/hourly scheduler implementation.
- Lever, generic HTML, or additional source adapters.
- Large-dataset performance validation and development seed data.

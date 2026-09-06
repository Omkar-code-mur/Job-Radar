# Implementation Plan: Greenhouse Public Job Ingestion

**Branch**: `001-job-radar-v1-mvp` | **Date**: 2026-08-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-job-radar-v1-mvp/spec.md`, with the
Greenhouse public ingestion slice prioritized for this implementation.

**Reconciliation**: See [reconciliation.md](reconciliation.md) for the verified repository state,
requirement classifications, and the boundary between implemented behavior and this feature.

## Summary

Deliver a Greenhouse ingestion verification slice on the existing Job Radar product. The
slice fetches a permitted public Greenhouse board, normalizes valid jobs, upserts them using
the composite `(companyId, sourceId, externalJobId)` identity, updates `lastSeenAt`, records
source health, and continues aggregate scans after individual source failures. Matching,
notifications, email, and scheduled monitoring are outside this feature.

## Technical Context

**Language/Version**: C# / .NET 8 for the API; TypeScript for the React client

**Primary Dependencies**: ASP.NET Core minimal APIs, built-in `HttpClient` and JSON
serialization, React/Vite client, existing OpenAPI-generated TypeScript client

**Storage**: Supabase PostgreSQL through `PostgresJobRadarStore` and
`UserScopedJobRadarStore`; PostgreSQL is already the active persistence implementation

**Testing**: .NET test project using xUnit; focused adapter, normalization, composite-identity
upsert, last-seen updates, failure-isolation, source-health, and API contract tests

**Target Platform**: Cross-platform .NET 8 server and browser-based React application

**Project Type**: Web application with a separately deployable REST API and frontend

**Performance Goals**: No new performance target is introduced by this slice. Existing behavior
must remain responsive enough to expose valid fetched jobs after the scan; formal large-dataset
performance validation is deferred.

**Constraints**: Public Greenhouse board endpoints only; 10-second request timeout; bounded
retry with backoff; truthful user-agent; no authentication or anti-bot bypass; no AI calls;
existing `/api` response shapes remain compatible with the frontend

**Scale/Scope**: Existing authenticated workspaces, manually configured sources, and an initial
target of 5-10 monitored companies; this slice implements and verifies Greenhouse only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

PASS: The design keeps source fetching, normalization, persistence, and source-health decisions
behind the existing boundaries. It uses a public documented endpoint, preserves the existing
API contract, avoids protected-source access and AI calls, and supports focused tests.

PASS: The implementation uses the existing PostgreSQL store and user-scoped match persistence.
No in-memory replacement is part of this feature.

PASS: Fetch failures are bounded, observable through source health, and isolated from other
sources. The aggregate scan continues after a source failure. No protected-source access or
bypass behavior is permitted.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── reconciliation.md    # Repository-state reconciliation
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
```text
artifacts/
├── api-server-dotnet/
│   ├── JobRadar.Api.csproj
│   ├── Program.cs
│   ├── Auth/
│   ├── Database/
│   ├── Properties/launchSettings.json
│   └── Sources/
└── job-radar/
  ├── src/
  └── vite.config.ts

lib/api-spec/
└── openapi.yaml

lib/api-client-react/
└── src/generated/

specs/001-job-radar-v1-mvp/
├── plan.md
├── research.md
├── data-model.md
├── reconciliation.md
├── contracts/
├── quickstart.md
└── tasks.md
```

**Structure Decision**: Keep the separately deployable React frontend and ASP.NET Core API
under `artifacts/`, with the existing OpenAPI contract and generated client under `lib/`.
Greenhouse source and normalization logic belong in the API project; the frontend consumes
the unchanged `/api` contract through its Vite development proxy.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | The design stays within the modular monolith boundary. |

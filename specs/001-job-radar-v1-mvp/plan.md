# Implementation Plan: Greenhouse Public Job Ingestion

**Branch**: `001-job-radar-v1-mvp` | **Date**: 2026-08-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-job-radar-v1-mvp/spec.md`, with the
Greenhouse public ingestion slice prioritized for this implementation.

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Deliver a first real-source vertical slice that reads a permitted public Greenhouse board,
normalizes its jobs into the existing Job Radar contract, upserts them idempotently in the
current development store, evaluates them with deterministic matching, and exposes the
existing scan endpoints to the React dashboard. The source adapter and monitoring workflow
will be isolated so PostgreSQL persistence and additional source adapters can replace the
development store later.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# / .NET 8 for the API; TypeScript for the React client

**Primary Dependencies**: ASP.NET Core minimal APIs, built-in `HttpClient` and JSON
serialization, React/Vite client, existing OpenAPI-generated TypeScript client

**Storage**: Existing in-memory development store behind a repository-shaped service boundary;
PostgreSQL is a follow-up persistence phase and is not required for this slice

**Testing**: .NET test project using xUnit; focused adapter, normalization, deduplication,
failure-isolation, and API contract tests

**Target Platform**: Cross-platform .NET 8 server and browser-based React application

**Project Type**: Web application with a separately deployable REST API and frontend

**Performance Goals**: A single configured board scan completes within 30 seconds under normal
conditions; valid fetched jobs are available to the jobs view immediately after the scan

**Constraints**: Public Greenhouse board endpoints only; 10-second request timeout; bounded
retry with backoff; truthful user-agent; no authentication or anti-bot bypass; no AI calls;
existing `/api` response shapes remain compatible with the frontend

**Scale/Scope**: One personal profile, manually configured sources, and an initial target of
5-10 monitored companies; this slice implements Greenhouse only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

PASS: The design keeps source fetching, normalization, persistence, matching, and notification
decisions behind separate boundaries. It uses a public documented endpoint, preserves the
existing API contract, avoids AI/cloud requirements, and supports focused tests.

PASS: The first implementation uses the existing in-memory store only as a development
repository. The persistence boundary remains explicit so the later PostgreSQL migration does
not change source or matching behavior.

PASS: Fetch failures are bounded, observable through source health, and isolated from other
sources. No protected-source access or bypass behavior is permitted.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
artifacts/
├── api-server-dotnet/
│   ├── JobRadar.Api.csproj
│   ├── Program.cs
│   └── Properties/launchSettings.json
└── job-radar/
  ├── src/
  └── vite.config.ts

lib/api-spec/
└── openapi.yaml

specs/001-job-radar-v1-mvp/
├── plan.md
├── research.md
├── data-model.md
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

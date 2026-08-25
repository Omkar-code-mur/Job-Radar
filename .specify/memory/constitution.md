<!-- @format -->

<!--
Sync Impact Report
- Version change: scaffold/unversioned -> 1.0.0
- Modified principles: none; this is the initial project constitution.
- Added sections: Product Scope and Constraints; Development and Quality Gates.
- Removed sections: none; scaffold placeholders were replaced.
- Follow-up TODOs: confirm the original ratification date in TODO(RATIFICATION_DATE).
-->

# Job Radar Constitution

## Core Principles

### I. Modular, Understandable Design

Job Radar MUST use a modular monolith with clear boundaries between source adapters,
normalization, persistence, application services, matching, notifications, and scheduling.
Core workflows MUST depend on interfaces such as `IJobSource`, `IJobRepository`,
`IMatchingEngine`, and `INotificationService` rather than infrastructure implementations.
Each feature MUST remain understandable, independently testable, and runnable after its
incremental delivery. This preserves maintainability and keeps future source, matcher, or
deployment changes localized.

### II. Source Independence and Data Integrity

Every external job source MUST be implemented behind `IJobSource` and converted into the
common normalized job model before application logic uses it. Adapters MUST support only
publicly accessible pages or documented public APIs and MUST preserve useful raw metadata.
Ingestion MUST be idempotent: the database MUST enforce uniqueness for
`companyId + sourceId + externalJobId`, and repeated scans MUST update existing jobs without
creating duplicates or repeated notifications.

### III. Transparent Rule-Based Matching

V1 MUST use deterministic `RuleBasedMatcher` behavior only; it MUST NOT call LLMs,
embeddings, or semantic AI services. Matching MUST implement `IMatchingEngine` and return a
structured score, breakdown, matched criteria, missing criteria, and reasons. Weights,
thresholds, include keywords, exclude keywords, and profile settings MUST be validated and
persisted rather than hidden in code. A user MUST be able to understand why a job matched or
did not match.

### IV. Reliability, Safety, and Respectful Access

The monitoring pipeline MUST isolate source failures so one unavailable or malformed source
does not stop other sources. Network access MUST use timeouts, bounded retries with backoff,
reasonable rate limits, and a truthful user-agent. The system MUST never bypass login,
CAPTCHA, anti-bot controls, private APIs, or other access controls. Fetch attempts,
successes, failures, timing where useful, and notification outcomes MUST be observable.

### V. Focused Delivery and Verifiable Behavior

The project MUST advance incrementally from the existing UI and API toward PostgreSQL
persistence, real source adapters, matching, notifications, scheduling, and operational
hardening. New business behavior MUST have focused tests, especially matcher rules,
normalization, deduplication, and notification suppression. Changes MUST preserve the
existing API contract unless a documented migration is supplied. Complexity MUST be justified
by a current requirement; V1 MUST NOT introduce AI infrastructure, Azure dependencies,
browser automation, bypass mechanisms, microservices, or unnecessary queues.

## Product Scope and Constraints

Job Radar V1 is a personal job-monitoring platform for manually configured companies and
public job sources. It MUST provide company and source management, normalized and persistent
jobs, candidate profiles, configurable transparent matching, search and filtering, job
details, notification history, source health, manual scans, and an approximately hourly
scheduled scan. PostgreSQL is the persistence target, REST/OpenAPI is the service contract,
and React with TypeScript is the web client. Credentials and operational configuration MUST
come from environment variables or workspace secrets; they MUST NOT be hardcoded.

V1 excludes LLMs, embeddings, semantic matching, AI agents, browser automation, CAPTCHA or
anti-bot bypass, private or undocumented APIs, Kubernetes, microservices, and mandatory
Azure services. Future implementations MAY add `AIJobMatcher` or provider abstractions only
behind the existing matching boundary and without coupling core business logic to a vendor.

## Development and Quality Gates

Every change MUST identify its owning module and keep unrelated files untouched. Before
implementation, the intended behavior and affected contract MUST be clear. Before merge, the
relevant tests, type checks, lint checks, migrations, and API documentation MUST pass or any
known exception MUST be recorded. Database changes MUST include migrations, foreign keys,
indexes, and seed behavior where applicable. Production-facing workflows MUST expose useful
structured logs and actionable errors. Demo seed data MUST be clearly distinguishable from
live job data.

## Governance

This constitution is the governing policy for project design and delivery. A proposed
amendment MUST state the affected principle or section, motivation, compatibility impact, and
any migration or documentation work. Amendments MUST update the Sync Impact Report and the
version metadata in the same change. Reviews MUST check compliance with the principles,
scope constraints, security and source-access rules, test coverage, and operational behavior.

Constitution versions use semantic versioning: MAJOR for incompatible principle removals or
redefinitions, MINOR for new principles or materially expanded governance, and PATCH for
clarifications and non-semantic corrections. The constitution MUST be reviewed whenever a
feature spec, implementation plan, or task list is created or materially changed.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): confirm original adoption date | **Last Amended**: 2026-08-25

# Job Radar

Job Radar is a personal monitoring dashboard for public company career pages. It keeps a focused view of new jobs, scores them against transparent candidate preferences, and records which match alerts have been sent.

## What is included

- Dashboard with recent high-match roles and source health
- Company and source management
- Job search and filters
- Job detail pages with score breakdowns and matched/missing skills
- Candidate profile and matching-weight settings
- Notification history
- Manual scan for one source or all enabled sources
- Seeded development data so the app is useful immediately

V1 intentionally does not use AI, LLMs, embeddings, browser automation, or private APIs.

## Run locally

Prerequisites: Node.js 20+ and pnpm 10+.

```bash
pnpm install
pnpm --filter @workspace/api-server run dev
pnpm --filter @workspace/job-radar run dev
```

The API is served at `/api`; the Job Radar web app is served at `/`.

Useful checks:

```bash
pnpm run typecheck
pnpm --filter @workspace/api-server run typecheck
pnpm --filter @workspace/job-radar run typecheck
```

The current development store is seeded in memory so the preview works without external provider credentials. The storage boundary is isolated in the API server and can be replaced with PostgreSQL repositories when persistence is enabled.

## Architecture

The application is organized around this pipeline:

```text
JobSource adapters -> fetching -> normalization -> persistence -> filtering
  -> IMatchingEngine -> notifications
```

The matching boundary is deliberately small:

```ts
interface IMatchingEngine {
  match(job: NormalizedJob, profile: CandidateProfile): Promise<MatchResult>;
}
```

`RuleBasedMatcher` is the V1 implementation. A future `AIJobMatcher` or `HybridMatcher` can implement the same interface and be selected by the application service without changing job entities, source adapters, dashboard queries, or notification history.

## Source safety

Only publicly accessible sources should be configured. The product must not bypass authentication, CAPTCHAs, anti-bot systems, private APIs, or robots/access restrictions. Unsupported or failed sources should surface a descriptive status rather than fabricate jobs.

## Future email configuration

Email delivery belongs behind an `INotificationService` abstraction. When SMTP is enabled, keep SMTP credentials in environment variables or workspace secrets; never commit them.

## API contract

`lib/api-spec/openapi.yaml` is the source of truth. After changing it, regenerate the typed client and Zod schemas:

```bash
pnpm --filter @workspace/api-spec run codegen
```
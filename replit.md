# Job Radar

Personal job monitoring for public career sources, with transparent rule-based matching and notification history.

## Run & Operate

- `pnpm --filter @workspace/api-server run dev` — run the API server (port 5000)
- `pnpm run typecheck` — full typecheck across all packages
- `pnpm run build` — typecheck + build all packages
- `pnpm --filter @workspace/api-spec run codegen` — regenerate API hooks and Zod schemas from the OpenAPI spec
- `pnpm --filter @workspace/db run push` — push DB schema changes (dev only)
- Required env: `DATABASE_URL` — Postgres connection string

## Stack

- pnpm workspaces, Node.js 24, TypeScript 5.9
- API: Express 5
- DB: PostgreSQL + Drizzle ORM
- Validation: Zod (`zod/v4`), `drizzle-zod`
- API codegen: Orval (from OpenAPI spec)
- Build: esbuild (CJS bundle)

## Where things live

- `artifacts/job-radar` — React/Vite web app and all user-facing routes
- `artifacts/api-server/src/routes/job-radar.ts` — REST handlers
- `artifacts/api-server/src/lib/job-radar-store.ts` — seeded development data and storage boundary
- `artifacts/api-server/src/domain/matching.ts` — `IMatchingEngine` and V1 `RuleBasedMatcher`
- `lib/api-spec/openapi.yaml` — API contract source of truth

## Architecture decisions

- V1 matching is deterministic and explainable; no AI or embeddings are used.
- Source management uses a common source shape so Greenhouse, Lever, structured HTML, and generic HTML adapters can be added independently.
- The matcher is accessed through `IMatchingEngine`, leaving room for AI or hybrid implementations later.
- Development seed data keeps the preview useful without requiring provider credentials.

## Product

The app monitors public job sources, normalizes roles into a shared job model, scores them against candidate preferences, and shows recent opportunities, source health, and notification history.

## User preferences

_Populate as you build — explicit user instructions worth remembering across sessions._

## Gotchas

- Run `pnpm --filter @workspace/api-spec run codegen` after changing the OpenAPI contract.
- The API and frontend are separate managed services; restart both after changing service configuration.
- Development data is currently in memory and resets when the API process restarts.

## Pointers

- See the `pnpm-workspace` skill for workspace structure, TypeScript setup, and package details

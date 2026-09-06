<!-- @format -->

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
- Supabase authentication with application-level USER / ADMIN roles
- Profile fast-track import
- Optional, user-triggered AI job intelligence
- Database-backed workspace data configured through Supabase

AI is intentionally not part of automatic scheduler scans. It runs only when the user explicitly selects **Analyze with AI** for a job. The analysis returns a structured verdict, fit score, summary, strengths, gaps, concerns, interview focus, and recommended next action.

## Run locally

Prerequisites: Node.js 22+ and npm 10+.

```bash
npm install
```

On Windows, start the backend in its own terminal so API logs remain separate from your working terminal:

```powershell
.\scripts\run-backend.cmd
```

The equivalent direct command is:

```bash
dotnet run --project artifacts/api-server-dotnet/JobRadar.Api.csproj
```

Then start the web app in your original terminal:

```bash
npm run dev --workspace=@workspace/job-radar
```

The ASP.NET Core API runs at `http://localhost:5000/api`; the Job Radar web app runs at
`http://localhost:5173` and proxies `/api` requests to the backend.

The API uses Supabase PostgreSQL. For local credentials, initialize .NET User Secrets once
and store the database connection string and Supabase JWT secret outside the repository.

For the browser, copy `artifacts/job-radar/.env.example` to `.env.local` and fill in the same
Supabase project URL plus the project's public anon key. Never commit real credentials.

## Verification

Run locally before merging:

```bash
dotnet build artifacts/api-server-dotnet/JobRadar.Api.csproj
npm run typecheck --workspace=@workspace/job-radar
npm run build --workspace=@workspace/job-radar
```

GitHub Actions runs the backend build plus frontend typecheck/build for pull requests and pushes to `main`.

## Architecture

The application is organized around this pipeline:

```text
Supabase Auth -> ASP.NET Core JWT validation -> users (USER / ADMIN)
                                      |
JobSource adapters -> fetching -> normalization -> persistence -> filtering
  -> IMatchingEngine -> notifications
                                      |
                              Analyze with AI
                                      |
                         IAiJobIntelligenceProvider
                           /                    \
                 OpenAI-compatible          future providers
```

The matching boundary is deliberately small:

```ts
interface IMatchingEngine {
  match(job: NormalizedJob, profile: CandidateProfile): Promise<MatchResult>;
}
```

`RuleBasedMatcher` remains the deterministic V1 implementation. AI is a separate intelligence layer rather than a replacement for the matcher or scheduler.

## AI configuration

The AI provider is backend-only and optional. Never expose provider API keys to the browser.

Configuration can be supplied through .NET configuration/environment variables. The current provider supports the OpenAI Responses API; provider-specific adapters can be added behind `IAiJobIntelligenceProvider` without changing the UI contract.

No AI key is required for normal Job Radar scanning and matching.

## Source safety

Only publicly accessible sources should be configured. The product must not bypass authentication, CAPTCHAs, anti-bot systems, private APIs, or robots/access restrictions. Unsupported or failed sources should surface a descriptive status rather than fabricate jobs.

## Future email configuration

Email delivery belongs behind an `INotificationService` abstraction. When SMTP is enabled, keep SMTP credentials in environment variables or workspace secrets; never commit them.

## API contract

`lib/api-spec/openapi.yaml` is the source of truth. After changing it, regenerate the typed client and Zod schemas:

```bash
npm run codegen --workspace=@workspace/api-spec
```

<!-- @format -->

# Job Radar — Complete Project Context

## 1. Project Overview

I am building a personal project called **Job Radar**.

The goal is to create a personal job-monitoring platform that automatically watches **manually configured company career pages and public job APIs**, discovers newly posted jobs, evaluates them against my predefined career preferences, and notifies me by email when a newly discovered job is a strong match.

The long-term vision is to make Job Radar significantly more useful than a normal job-board search by combining:

- company-specific career monitoring
- multiple job sources
- reliable job normalization
- duplicate detection
- transparent rule-based matching in V1
- later semantic/AI matching in V2
- personalized ranking
- explanations for why a job matches
- missing-skill detection
- notifications only for genuinely relevant new jobs

The product should eventually become a real deployable application under my domain:

**graphforge.in**

However, the immediate objective is to build a reliable **V1 MVP with zero/near-zero cost**, understand the architecture deeply, and use the project as both a portfolio project and an interview-learning project.

---

# 2. Important Development Philosophy

This is not intended to be a one-shot AI-generated demo.

I am a developer and want to understand, maintain, debug, and extend the generated code.

The AI coding agent should therefore:

- create clean modular code
- avoid unnecessary complexity
- explain important architectural decisions
- preserve clear interfaces
- avoid giant monolithic files
- avoid over-engineering
- keep the project understandable
- implement features incrementally
- keep the application runnable after every phase
- prefer simple production-oriented architecture over unnecessary enterprise infrastructure

The AI agent should **not** build everything in one huge generation step if doing so compromises maintainability.

---

# 3. Current Development Status

I initially used **Replit free tier** to bootstrap the project.

The current Replit-generated project already has a polished dashboard UI and basic architecture.

The current README describes:

- Dashboard
- Company management
- Source management
- Job search/filtering
- Job details
- Candidate profile
- Matching settings
- Notification history
- Manual scan
- Source health
- Seeded development data

The current development store is **in memory**, so persistent PostgreSQL storage has not yet been implemented.

The current architecture is already organized around:

```text
JobSource adapters
    ↓
Fetching
    ↓
Normalization
    ↓
Persistence
    ↓
Filtering
    ↓
IMatchingEngine
    ↓
Notifications
```

The matching abstraction already looks conceptually like:

```ts
interface IMatchingEngine {
  match(job: NormalizedJob, profile: CandidateProfile): Promise<MatchResult>;
}
```

The V1 implementation is:

```text
RuleBasedMatcher
```

The future architecture should support:

```text
IMatchingEngine
    ├── RuleBasedMatcher
    ├── AIJobMatcher
    └── HybridMatcher
```

The current Replit project uses a pnpm workspace and currently has packages/apps represented by names such as:

```text
@workspace/api-server
@workspace/job-radar
@workspace/api-spec
```

The existing README indicates:

```bash
pnpm install

pnpm --filter @workspace/api-server run dev
pnpm --filter @workspace/job-radar run dev
```

The API is served under `/api` and the web application under `/`.

There is also an OpenAPI specification at:

```text
lib/api-spec/openapi.yaml
```

with code generation for typed client/Zod schemas.

Do not unnecessarily throw away the existing project. Continue from the existing architecture where it is sound.

---

# 4. Current UI Direction

The current dashboard has a modern SaaS/product-style design.

The product is branded:

**Job Radar**

The dashboard currently uses the concept:

**"Signal Desk"**

The main idea is that the user should see the jobs worth attention without manually scrolling through hundreds of listings.

Current navigation:

```text
MONITOR

Overview
Jobs
Companies
Sources

TUNE

Candidate Profile
Matching Rules
Notifications
Source Health
```

Dashboard concepts include:

- Companies watched
- Sources live
- New jobs
- High-signal matches
- Alerts sent
- Recent high-match roles
- Source health
- Last check
- Manual "Scan all"

Keep this visual direction unless there is a strong usability reason to change it.

The UI should feel like a real focused productivity/SaaS product, not a generic admin template.

---

# 5. V1 Scope

V1 must be completely functional **without AI**.

V1 should support:

1. Company management
2. Career-source management
3. Public job API integration
4. Supported structured career-page ingestion
5. Job normalization
6. Job persistence
7. Job deduplication
8. Candidate profile
9. Configurable matching rules
10. Transparent rule-based matching
11. Match explanations
12. Job filtering/search
13. Notification history
14. Email notifications
15. Scheduled job scanning
16. Manual scanning
17. Source health monitoring
18. Error handling
19. Logging
20. Retries
21. Rate limiting/respectful fetching
22. Database migrations
23. Seed data
24. API documentation
25. README/setup instructions
26. Tests for important business logic

---

# 6. Explicitly NOT in V1

Do NOT implement:

- LLMs
- embeddings
- semantic AI matching
- AI agents
- browser automation
- CAPTCHA bypass
- anti-bot bypass
- private APIs
- authentication bypass
- scraping protected/internal systems
- Kubernetes
- microservices
- complex distributed architecture
- unnecessary message queues
- unnecessary cloud infrastructure

The first goal is a reliable modular monolith.

---

# 7. Future V2 AI Vision

The architecture must make V2 easy.

The future should look like:

```text
Job Sources
    ↓
Fetching
    ↓
Normalization
    ↓
Persistence
    ↓
Filtering
    ↓
IMatchingEngine
    ├── RuleBasedMatcher
    ├── AIJobMatcher
    └── HybridMatcher
    ↓
Notifications
```

The rest of the application must not care which matching implementation is active.

The application should depend on:

```ts
IMatchingEngine;
```

rather than:

```ts
RuleBasedMatcher;
```

directly.

The future `AIJobMatcher` should eventually be capable of:

- semantic job/profile matching
- understanding equivalent skills
- recognizing terminology differences
- determining whether a role is genuinely relevant
- ranking jobs
- explaining why a job matches
- identifying missing skills
- using LLMs
- using embeddings
- combining deterministic and semantic scores

Potential future AI providers include:

- Azure OpenAI
- Gemini
- Groq
- Mistral
- GitHub Models
- other OpenAI-compatible providers

No provider should be hardcoded into the core business logic.

Future credentials must use environment variables/secrets.

Potential future architecture:

```text
IAIProvider
    ├── AzureOpenAIProvider
    ├── GeminiProvider
    ├── GroqProvider
    ├── MistralProvider
    └── OpenAICompatibleProvider
```

But this should NOT be implemented prematurely in V1.

---

# 8. Recommended V1 Technology Stack

Use a modern TypeScript full-stack stack.

Preferred:

### Frontend

- React
- TypeScript
- modern component-based UI
- responsive design

### Backend

- Node.js
- TypeScript
- REST API

### Database

- PostgreSQL

### ORM

- Prisma or another well-supported TypeScript ORM

### Validation

- Zod or equivalent

### API contract

- OpenAPI
- generated typed client/schema where appropriate

### Email

Use an abstraction:

```text
INotificationService
```

V1 implementation:

```text
EmailNotificationService
```

Use SMTP or another simple email provider.

Credentials must come from environment variables/workspace secrets.

### Scheduler

Initially use a simple application/background scheduler rather than Azure Functions.

The scheduler should be abstracted so that it can later be moved to Azure Functions without changing business logic.

### Deployment

Initially optimize for free/low-cost development.

Do not introduce Azure just because the final production architecture may use Azure.

---

# 9. Azure Decision

Azure is NOT required for V1.

The initial objective is to build the working product at zero/near-zero cost.

Do not make Azure Functions, Azure Database, Azure OpenAI, Azure AI Search, etc. mandatory dependencies for V1.

However, the architecture should allow a future Azure deployment.

Eventually the scheduler could become:

```text
Azure Function
    ↓
JobMonitoringService
```

The Azure Function should only trigger the business workflow.

Business logic should remain in application services:

```text
Azure Function
    ↓
JobMonitoringService
    ↓
IJobSource
    ↓
Normalizer
    ↓
Repository
    ↓
IMatchingEngine
    ↓
INotificationService
```

Do not put the entire application inside an Azure Function.

---

# 10. Target Architecture

Use a modular monolith.

Conceptually:

```text
                    React Frontend
                          │
                          ↓
                     REST API
                          │
                          ↓
                 Application Services
                          │
        ┌─────────────────┼─────────────────┐
        ↓                 ↓                 ↓
   Source System      Matching System   Notification
        │                 │                 │
        ↓                 ↓                 ↓
  Job Sources       IMatchingEngine     Email Service
        │
        ↓
    Normalizer
        │
        ↓
   Repository Layer
        │
        ↓
    PostgreSQL
```

---

# 11. Job Source Architecture

Create a common interface such as:

```ts
interface IJobSource {
  fetchJobs(source: JobSourceConfig): Promise<RawJob[]>;
}
```

Source-specific adapters should implement this contract.

Potential implementations:

```text
GreenhouseSource
LeverSource
StructuredHtmlSource
GenericHtmlSource
```

The rest of the application must not need to know source-specific API response structures.

---

# 12. Supported Source Types

Each company can have multiple sources.

Example:

```text
Microsoft
    ├── Career Page
    └── Public API

Atlassian
    └── Public API

Another Company
    └── Structured Career Page
```

Source configuration should include:

- company
- source name
- source type
- URL
- enabled/disabled
- configuration
- last successful fetch
- last attempted fetch
- failure count
- last error
- created timestamp
- updated timestamp

Initial source types:

```text
GREENHOUSE_API
LEVER_API
STRUCTURED_HTML
GENERIC_HTML
```

Design this so more adapters can be added later.

---

# 13. Job Ingestion Pipeline

The ingestion pipeline should be:

```text
Scheduled/manual trigger
        ↓
Find enabled sources
        ↓
Fetch raw jobs
        ↓
Normalize
        ↓
Validate
        ↓
Deduplicate/upsert
        ↓
Identify newly discovered jobs
        ↓
Run matching
        ↓
Persist match
        ↓
Notify if threshold passed
        ↓
Record notification
        ↓
Update source health
```

One failed source must not stop all other sources.

---

# 14. Common Normalized Job Model

All sources must be converted into a common job model.

At minimum:

```text
Job
├── id
├── companyId
├── sourceId
├── externalJobId
├── title
├── description
├── location
├── workplaceType
├── department
├── employmentType
├── postedDate
├── applicationUrl
├── sourceUrl
├── firstSeenAt
├── lastSeenAt
├── createdAt
├── updatedAt
└── rawMetadata
```

Additional source-specific information can be retained in JSON metadata where appropriate.

---

# 15. Deduplication

A job must not be inserted repeatedly every time the hourly scan runs.

Use a unique database constraint based on:

```text
companyId + sourceId + externalJobId
```

The ingestion process should behave idempotently.

For example:

```text
First scan:
Job A → INSERT

Second scan:
Job A → existing record → UPDATE/IGNORE

Third scan:
Job A → existing record → UPDATE/IGNORE
```

Do not send a new notification just because the same job was fetched again.

---

# 16. Candidate Profile

The candidate profile should support:

### Preferred roles

Examples:

```text
Full Stack Developer
Software Engineer
AI Engineer
Backend Developer
Frontend Developer
```

### Skills

Examples:

```text
React
.NET
C#
TypeScript
Azure
SQL
Semantic Kernel
Azure OpenAI
```

### Technologies

Allow technologies to be configured separately where useful.

### Experience

- minimum years
- maximum years

### Preferred locations

Examples:

```text
Pune
Mumbai
Bangalore
Remote
```

### Workplace preference

```text
Remote
Hybrid
On-site
Any
```

### Include keywords

### Exclude keywords

### Minimum match score

Example:

```text
70 / 100
```

Persist all settings in PostgreSQL.

---

# 17. Rule-Based Matching

Create:

```ts
interface IMatchingEngine {
  match(job: NormalizedJob, profile: CandidateProfile): Promise<MatchResult>;
}
```

V1 implementation:

```text
RuleBasedMatcher
```

The result should be structured.

Example:

```ts
interface MatchResult {
  score: number;
  isMatch: boolean;
  matchedCriteria: string[];
  missingCriteria: string[];
  reasons: string[];
  breakdown: {
    roleRelevance: number;
    skills: number;
    experience: number;
    location: number;
    aiRelevance: number;
    freshness: number;
  };
}
```

---

# 18. V1 Matching Criteria

Score jobs using transparent deterministic rules.

### Role/title relevance

Compare job title with preferred roles.

Example:

```text
Preferred:
Full Stack Developer

Job:
Full Stack Software Engineer
```

This should receive a strong title match based on keyword/phrase rules.

Do NOT pretend this is semantic AI.

---

### Skill matching

Search the job title/description/requirements/responsibilities for configured skills.

Example:

```text
Candidate:
React
.NET
Azure
SQL

Job:
React
C#
.NET 8
Azure
```

Determine matched skills.

Missing skills should be shown when detectable.

---

### Experience

Detect common patterns:

```text
2+ years
2-4 years
3 years experience
minimum 2 years
```

Compare against candidate profile.

If experience cannot be detected, do not automatically treat it as a mismatch.

---

### Location

Compare:

- preferred cities
- remote preference
- hybrid preference
- on-site preference

---

### AI/GenAI relevance

V1 uses only explicit keywords.

Examples:

```text
AI
GenAI
Generative AI
LLM
Azure OpenAI
OpenAI
Semantic Kernel
RAG
Agentic AI
Machine Learning
```

No AI model should be called.

---

### Freshness

Newer jobs should receive a configurable freshness score.

---

# 19. Configurable Matching Weights

Do not hardcode scoring weights.

Example:

```text
Role relevance       30%
Skills               30%
Experience            15%
Location              10%
AI relevance          10%
Freshness              5%
```

Total:

```text
100%
```

The user should be able to configure:

- weights
- minimum threshold
- include keywords
- exclude keywords

The backend must validate the configuration.

---

# 20. Notifications

Create:

```text
INotificationService
```

V1:

```text
EmailNotificationService
```

When a newly discovered job crosses the configured threshold, send an email.

Email contents:

- job title
- company
- location
- workplace type
- experience requirement if detected
- match score
- score breakdown
- matched skills/criteria
- missing skills if detectable
- posted date
- direct application URL

---

# 21. Prevent Repeated Emails

A job should not be emailed repeatedly for the same candidate profile unless explicitly configured otherwise.

Create a notification history model.

Example:

```text
Notification
├── id
├── jobId
├── profileId
├── type
├── status
├── sentAt
├── error
└── createdAt
```

Before sending:

```text
Has this job already been successfully notified for this profile?
    ↓
YES → do not send
NO  → send → record notification
```

If an email fails, record the failure and allow controlled retry.

---

# 22. Scheduler

The application needs an hourly monitoring process.

Conceptually:

```text
Every hour
    ↓
JobMonitoringService.run()
```

The service should:

1. Find enabled sources
2. Fetch jobs
3. Normalize
4. Validate
5. Upsert
6. Identify new jobs
7. Match
8. Save match results
9. Notify
10. Record fetch/source status

There should also be a manual scan option.

The UI should support:

```text
Scan one source
Scan all enabled sources
```

The scheduler should be runnable locally.

For example:

```bash
pnpm run scheduler
```

or an equivalent command.

---

# 23. Source Health

Track:

- last attempted fetch
- last successful fetch
- status
- failure count
- last error
- number of jobs fetched
- response/fetch timing where useful

Statuses could include:

```text
HEALTHY
WARNING
FAILED
DISABLED
UNSUPPORTED
```

An unavailable source should not break the rest of the scan.

---

# 24. Error Handling

Handle:

- HTTP failures
- timeout
- malformed API responses
- malformed HTML
- missing job fields
- rate limiting
- temporary source failures
- duplicate jobs
- database failures
- notification failures

Use reasonable retries with backoff.

Do not retry indefinitely.

---

# 25. Respectful Source Access

Only use:

- publicly accessible pages
- documented public APIs
- sources that permit automated access

Never:

- bypass login
- bypass CAPTCHA
- bypass anti-bot protection
- access private/internal APIs
- circumvent access controls

Use:

- request timeout
- rate limiting
- retry/backoff
- reasonable user-agent
- respectful request frequency

If a source is unsupported, show a clear status rather than attempting to bypass its protections.

---

# 26. Database

Move the current in-memory store to PostgreSQL.

At minimum create:

```text
users
candidate_profiles
companies
job_sources
jobs
job_matches
matching_configurations
notifications
fetch_logs
```

Use:

- migrations
- foreign keys
- indexes
- unique constraints
- timestamps

Important unique constraint:

```text
companyId + sourceId + externalJobId
```

The database layer should be hidden behind repository/storage abstractions where appropriate.

---

# 27. Recommended Repository Structure

The exact structure can be adapted to the existing Replit project, but aim for something conceptually similar to:

```text
apps/
  job-radar/
    src/
      components/
      pages/
      hooks/
      services/
      types/

  api-server/
    src/
      controllers/
      routes/
      services/
      domain/
      repositories/
      sources/
      matching/
      notifications/
      scheduler/
      normalization/
      validation/
      logging/

packages/
  api-spec/
  shared/
```

Do not force this exact structure if the current Replit repository already has a good equivalent.

The important thing is **separation of responsibilities**, not folder-name perfection.

---

# 28. Application Services

Important services should include concepts such as:

```text
JobMonitoringService
JobIngestionService
JobNormalizationService
MatchingService
NotificationService
SourceHealthService
```

The scheduler should trigger the application service rather than containing business logic itself.

---

# 29. API

REST API should cover concepts such as:

```text
/auth
/companies
/sources
/jobs
/jobs/:id
/profile
/matching
/notifications
/dashboard
/scheduler
```

Use:

- proper HTTP status codes
- validation
- consistent error responses
- pagination
- filtering
- sorting

Maintain OpenAPI documentation.

---

# 30. Dashboard

Dashboard metrics should include:

```text
Companies monitored
Active sources
Jobs discovered
New jobs
Matched jobs
Jobs emailed
Failed sources
Recent high-match jobs
```

Recent matches should show:

```text
Job title
Company
Location
Workplace type
Match score
Posted date
Matched skills
Application URL
```

---

# 31. Jobs Page

Allow:

- search
- company filter
- source filter
- location filter
- workplace type
- posted date
- match score
- matched/not matched
- notified/not notified

Use pagination rather than loading an unlimited number of jobs.

---

# 32. Job Details

Show:

- title
- company
- location
- workplace type
- department
- employment type
- description
- posted date
- first seen date
- application URL
- source
- match score
- scoring breakdown
- matched skills
- missing skills
- matching reasons
- notification status

---

# 33. Required Pages

Maintain:

```text
Dashboard
Jobs
Job Details
Companies
Sources
Candidate Profile
Matching Rules
Notifications
Source Health
```

---

# 34. Seed Data

Provide development seed data so the UI works immediately.

Seed:

- example companies
- example sources
- example jobs
- candidate profile
- matching configuration
- example matches
- notification examples

Clearly mark seed data as development/demo data.

Do not confuse seeded data with actual live jobs.

---

# 35. Testing

Prioritize tests around business logic.

### Matcher tests

Test:

- title match
- skill match
- missing skills
- experience compatibility
- location compatibility
- remote preference
- excluded keywords
- AI/GenAI keyword detection
- freshness
- scoring weights
- threshold

### Deduplication tests

Verify:

```text
same company
+
same source
+
same external job ID
=
same job
```

### Notification tests

Verify a previously notified job does not generate another email.

### Source normalization tests

Test representative Greenhouse/Lever/source responses.

---

# 36. Environment Configuration

Use:

```text
.env.example
```

Never hardcode:

- database credentials
- email credentials
- API keys
- future AI credentials

Future AI credentials must be environment variables/secrets.

---

# 37. Deployment Direction

Do not optimize for expensive production infrastructure yet.

Initial objective:

```text
Local development
        ↓
Free/low-cost hosted MVP
        ↓
graphforge.in
```

Later, production could potentially use Azure components such as:

```text
Frontend
Backend
PostgreSQL
Azure Function scheduler
Application monitoring
Key Vault
Azure OpenAI
```

But these should be introduced only when there is a real need.

---

# 38. Potential Future Azure Architecture

Eventually:

```text
                     graphforge.in
                           │
                           ↓
                    Web Application
                           │
                           ↓
                       Backend API
                           │
               ┌───────────┴───────────┐
               ↓                       ↓
          PostgreSQL             Azure Function
                                       │
                                  Hourly trigger
                                       ↓
                              JobMonitoringService
                                       ↓
                              Job Source Adapters
                                       ↓
                                  PostgreSQL
                                       ↓
                              IMatchingEngine
                               /            \
                     RuleBasedMatcher    AIJobMatcher
                                             ↓
                                      AI Provider
                                             ↓
                                  Azure OpenAI/etc.
                                       ↓
                                Notification Service
```

Do not build this entire architecture now.

The current code should simply make this future migration possible.

---

# 39. Company Database / Initial Sources

The long-term system should not require manually entering thousands of companies one by one.

Eventually, an initial company database can be imported from an existing public dataset/company list where licensing permits.

Potential initial data:

```text
Company
Career URL
Domain
Source type
Active status
```

However, do not make this a blocker for V1.

Start with a small manually configured set of companies and prove the entire pipeline works.

Then expand.

---

# 40. Initial Real-Source Strategy

Do NOT immediately attempt to crawl hundreds/thousands of companies.

Start with:

```text
5–10 companies
```

and get the full pipeline working.

Recommended progression:

```text
Phase 1
Seed data
    ↓

Phase 2
PostgreSQL
    ↓

Phase 3
One real Greenhouse source
    ↓

Phase 4
One real Lever source
    ↓

Phase 5
Structured HTML source
    ↓

Phase 6
More companies
    ↓

Phase 7
Hourly scheduler
    ↓

Phase 8
Email notifications
```

Only expand after the fundamentals are reliable.

---

# 41. Important Architectural Principle: Idempotency

The hourly scheduler may encounter the same job repeatedly.

Therefore operations should be safe to repeat.

For example:

```text
Fetch job
    ↓
Normalize
    ↓
Upsert
```

Running this 100 times should not create 100 jobs.

Likewise:

```text
Match job
    ↓
Check notification history
    ↓
Notify only if not already successfully notified
```

This is an important design requirement.

---

# 42. Important Architectural Principle: Separation of Concerns

Do not create code like:

```text
fetchJob()
    → parseHTML()
    → saveToDatabase()
    → calculateScore()
    → sendEmail()
```

inside one giant function.

Instead:

```text
Source Adapter
    ↓
Normalizer
    ↓
Repository
    ↓
Matching Engine
    ↓
Notification Service
```

Each responsibility should be independently testable.

---

# 43. Important Architectural Principle: Dependency Inversion

The application should depend on abstractions.

Examples:

```text
IJobSource
IJobRepository
IMatchingEngine
INotificationService
```

Concrete implementations can then be changed without rewriting the application.

For example:

```text
IMatchingEngine
    ↓
RuleBasedMatcher
```

can later become:

```text
IMatchingEngine
    ↓
AIJobMatcher
```

without changing:

```text
JobMonitoringService
Database
Dashboard
Notification history
```

---

# 44. Important Architectural Principle: Adapter Pattern

External job sources all have different APIs/data structures.

For example:

```text
Greenhouse response
Lever response
HTML page
```

should become:

```text
RawJob
    ↓
NormalizedJob
```

Each source adapter handles its own external format.

This keeps the core application source-independent.

---

# 45. Important Architectural Principle: Strategy Pattern

Matching is a strategy.

```text
IMatchingEngine
     │
     ├── RuleBasedMatcher
     ├── AIJobMatcher
     └── HybridMatcher
```

The application can select the implementation without changing the rest of the system.

---

# 46. Important Architectural Principle: Repository Pattern

The application should not directly depend on SQL everywhere.

Conceptually:

```text
JobService
    ↓
IJobRepository
    ↓
PostgresJobRepository
```

This keeps persistence concerns separate from business logic.

---

# 47. Interview/Portfolio Purpose

This project is also being built as a learning and interview-preparation project.

I want to understand what I am implementing, not just have AI generate code.

Important concepts I want to learn/revise through this project:

- full-stack architecture
- React
- TypeScript
- Node.js
- REST APIs
- PostgreSQL
- ORM
- migrations
- repositories
- dependency injection
- Strategy Pattern
- Adapter Pattern
- background jobs
- scheduling
- idempotency
- deduplication
- API integrations
- HTML parsing
- rate limiting
- retry/backoff
- logging
- error handling
- email integration
- environment variables
- testing
- deployment
- system design
- AI integration architecture
- LLM provider abstraction
- embeddings
- semantic matching
- RAG/AI concepts later

For important implementation decisions, provide short explanations of:

1. What we are doing
2. Why we are doing it
3. Alternatives
4. Trade-offs
5. How to explain it in an interview

---

# 48. Relationship to My Existing Technical Background

I already have professional experience with:

- React
- .NET 8
- SQL Server
- Azure
- Azure OpenAI
- Azure AI Search
- Cosmos DB
- Semantic Kernel
- prompt engineering
- AI functionality
- agent/plugin integration
- full-stack development

My recent project experience includes building AI functionality using React/Fluent UI on the frontend and .NET 8/Semantic Kernel/Azure OpenAI on the backend.

Job Radar is therefore also intended to expand my understanding of:

- Node.js/TypeScript full-stack architecture
- PostgreSQL
- background processing
- source adapters
- modular system design
- AI-provider abstraction
- production architecture

Do not assume I need beginner explanations for every programming concept.

Instead, connect new concepts to equivalent concepts I may already know from React/.NET/Azure when useful.

---

# 49. Current V1 Architecture Summary

The target V1 should look approximately like:

```text
                    JOB RADAR V1

                       React UI
                          │
                          ↓
                       REST API
                          │
                          ↓
                 Application Services
                          │
         ┌────────────────┼─────────────────┐
         │                │                 │
         ↓                ↓                 ↓
   Source System     Matching System   Notification
         │                │                 │
         ↓                ↓                 ↓
   IJobSource       IMatchingEngine    INotificationService
         │                │                 │
         ↓                ↓                 ↓
    Normalizer      RuleBasedMatcher     Email
         │
         ↓
    Repository
         │
         ↓
     PostgreSQL

                 ↑
                 │
             Scheduler
          approximately hourly
```

---

# 50. V2 Architecture Summary

```text
                    JOB RADAR V2

                       React UI
                          │
                          ↓
                       REST API
                          │
                          ↓
                 Application Services
                          │
        ┌─────────────────┼─────────────────┐
        ↓                 ↓                 ↓
    Job Sources      IMatchingEngine    Notifications
        │                 │
        │          ┌──────┼──────────┐
        │          ↓      ↓          ↓
        │       Rule     AI       Hybrid
        │      Matcher Matcher    Matcher
        │                 │
        │                 ↓
        │            IAIProvider
        │                 │
        │        ┌────────┼──────────┐
        │        ↓        ↓          ↓
        │     Azure     Gemini      Other
        │     OpenAI
        │
        ↓
    PostgreSQL
```

---

# 51. Development Order From Here

The current Replit prototype already has the UI, API structure, in-memory store, seed data, and matching abstraction.

Continue in this order:

## Step 1 — Inspect and stabilize existing code

Do not rewrite working components unnecessarily.

Understand:

- current folder structure
- API routes
- storage abstraction
- domain models
- matching implementation
- UI components
- OpenAPI generation

Fix architecture issues before adding large features.

## Step 2 — PostgreSQL

Replace the in-memory storage with PostgreSQL.

Add:

- schema
- migrations
- repositories
- seed script
- indexes
- constraints

Keep the API behavior compatible with the existing UI.

## Step 3 — Real job source

Implement one real public Greenhouse source.

Get:

```text
Fetch
→ Normalize
→ Persist
→ Deduplicate
→ Display
```

working end-to-end.

## Step 4 — Lever

Add Lever through another source adapter.

## Step 5 — Matching

Connect real newly discovered jobs to `RuleBasedMatcher`.

Persist:

```text
JobMatch
```

## Step 6 — Email

Implement:

```text
INotificationService
EmailNotificationService
```

and notification deduplication.

## Step 7 — Scheduler

Implement the hourly monitoring process.

## Step 8 — Source health/retries/logging

Add production-oriented reliability.

## Step 9 — Tests

Focus on core business logic.

## Step 10 — Deployment

Only after the application is reliable locally.

---

# 52. Final Instructions to GitHub Copilot

Treat this document as the product and architecture context for Job Radar.

Before modifying code:

1. Inspect the existing repository.
2. Understand the current Replit-generated architecture.
3. Do not rewrite working functionality unnecessarily.
4. Identify what already exists versus what is missing.
5. Propose the smallest implementation needed for the next milestone.
6. Keep the application runnable.
7. Preserve existing interfaces where they are well-designed.
8. Avoid premature AI/cloud infrastructure.
9. Avoid over-engineering.
10. Keep business logic independent of infrastructure.
11. Add tests for important business logic.
12. Update README/API documentation when behavior changes.

The immediate next major milestone is:

```text
CURRENT:

React
  ↓
API
  ↓
In-memory storage


TARGET:

React
  ↓
API
  ↓
Application Services
  ↓
Repositories
  ↓
PostgreSQL
```

After PostgreSQL persistence is stable, implement the first real public job source.

The ultimate goal is a reliable personal job-monitoring platform that can evolve from:

```text
V1:
Rule-based job monitoring
```

into:

```text
V2:
AI-assisted semantic job matching
```

without restructuring the core application.

Do not implement AI in V1.
Do not make Azure a V1 dependency.
Do not sacrifice maintainability for speed.
Build incrementally and keep the system understandable to a developer who will continue developing it manually.

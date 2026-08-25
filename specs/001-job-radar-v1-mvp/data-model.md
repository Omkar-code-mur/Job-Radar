# Data Model: Greenhouse Public Job Ingestion

## Career Source

Represents a manually configured permitted public source.

| Field | Type | Rules |
|---|---|---|
| id | string | Unique within Job Radar. |
| companyId | string | Required reference to a monitored company. |
| companyName | string | Denormalized display value. |
| name | string | Required user-facing source name. |
| type | enum | `GREENHOUSE_API` for this slice. |
| url | string | Required public board URL for display. |
| configuration | object | Requires non-empty `boardToken` for Greenhouse. |
| enabled | boolean | Disabled sources are never fetched. |
| status | enum | `healthy`, `warning`, `failed`, or `never_run`. |
| lastFetch | timestamp/string | Updated after an attempted scan. |
| jobsFetched | integer | Number of valid jobs returned by the last successful fetch. |
| failureCount | integer | Incremented for failed source attempts. |
| lastError | string/null | Actionable latest failure, if any. |

## Raw Greenhouse Job

External response item received from the public board endpoint.

| Field | Type | Rules |
|---|---|---|
| id | integer/string | Required stable external identifier. |
| title | string | Required non-empty title. |
| content | string | Optional HTML description when `content=true`. |
| location.name | string | Optional location. |
| departments[].name | string | Optional department list. |
| offices[].name | string | Optional office list. |
| updated_at | timestamp | Optional source update time. |
| absolute_url | string | Required public application URL. |
| metadata | object | Retained source fields where useful. |

## Normalized Job

The source-independent job record exposed to the existing application.

| Field | Type | Rules |
|---|---|---|
| id | string | Internal identifier. |
| companyId | string | Required company reference. |
| sourceId | string | Required source reference. |
| externalJobId | string | Greenhouse `id`; part of deduplication identity. |
| title | string | Required normalized title. |
| description | string | HTML-stripped or safely normalized content. |
| location | string | Empty or `Unknown` when absent. |
| workplaceType | enum | Derived conservatively; otherwise `Unknown`. |
| department | string | Joined department names or empty. |
| employmentType | string | Empty when unavailable from Greenhouse. |
| postedDate | timestamp/string | Source update date when available. |
| applicationUrl | string | Required absolute URL. |
| sourceUrl | string | Configured public board URL. |
| firstSeenAt | timestamp | Set only when inserted. |
| lastSeenAt | timestamp | Updated on every valid observation. |
| rawMetadata | object | Optional retained source metadata. |

## State Transitions

- `never_run -> healthy`: valid response received, including zero valid jobs.
- `healthy -> warning`: partial item failures or transient failures recovered by retry.
- `healthy/warning -> failed`: bounded retries exhausted or response cannot be used.
- Any enabled status -> `never_run` is not automatic; it represents an unscanned source.
- Existing job: update mutable fields and `lastSeenAt`; preserve `firstSeenAt`.
- New job: insert once and mark as eligible for matching/notification processing.

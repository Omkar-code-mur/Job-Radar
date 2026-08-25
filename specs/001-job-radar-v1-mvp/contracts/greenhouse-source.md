# Greenhouse Source Contract

## Configuration

A configured source of type `GREENHOUSE_API` MUST contain:

```json
{
  "companyId": "company-1",
  "name": "Example Careers",
  "type": "GREENHOUSE_API",
  "url": "https://boards.greenhouse.io/example",
  "configuration": {
    "boardToken": "example"
  }
}
```

`boardToken` is required. The source URL is retained for display and must be publicly
accessible. The adapter requests:

```text
https://boards-api.greenhouse.io/v1/boards/{boardToken}/jobs?content=true
```

## Response mapping

The adapter maps each valid item as follows:

- `id` -> `externalJobId`
- `title` -> `title`
- `content` -> normalized `description`
- `location.name` -> `location`
- `departments[].name` -> `department`
- `absolute_url` -> `applicationUrl`
- configured source URL -> `sourceUrl`
- `updated_at` -> `postedDate` when present

Missing optional values use empty strings or `Unknown`. Items without `id`, `title`, or
`absolute_url` are skipped and recorded as partial source diagnostics.

## Scan behavior

`POST /api/sources/{id}/scan` MUST:

- return the existing `ScanResult` shape;
- fetch only when the source exists and is enabled;
- upsert valid normalized jobs using `(companyId, sourceId, externalJobId)`;
- report valid jobs fetched and newly inserted jobs separately;
- update source health after the attempt;
- return an actionable error for missing, disabled, invalid, or unsupported configuration;
- never bypass authentication, CAPTCHA, anti-bot controls, or private endpoints.

`POST /api/scheduler/scan` MUST apply the same behavior to every enabled source and continue
processing when one source fails.

## Error behavior

- Invalid source configuration: HTTP 400.
- Unknown source or job: HTTP 404.
- Greenhouse transient failure after bounded retry: source marked `failed` or `warning`; other
  sources continue.
- Malformed individual job: skip item, retain diagnostic, and process remaining items.
- No credentials are accepted or required for this public source adapter.

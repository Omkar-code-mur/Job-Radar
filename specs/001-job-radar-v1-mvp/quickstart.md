# Quickstart: Greenhouse Public Job Ingestion

## Prerequisites

- Node.js 22+ and npm
- .NET 8 SDK
- Repository dependencies installed with `npm install`
- A public Greenhouse board token for a company that permits automated access

## Run the API

From the repository root:

```powershell
dotnet run --project artifacts/api-server-dotnet/JobRadar.Api.csproj --urls http://localhost:5000
```

Verify health:

```powershell
Invoke-RestMethod http://localhost:5000/api/healthz
```

Expected result:

```json
{"status":"ok"}
```

## Run the frontend

In a second terminal:

```powershell
npm run dev --workspace=@workspace/job-radar
```

Open `http://localhost:5173`.

## Validate the Greenhouse flow

1. Open **Companies** and add a monitored company.
2. Open **Sources** and add a `GREENHOUSE_API` source.
3. Set the source URL to the company's public Greenhouse board URL.
4. Set its `boardToken` in the source configuration used by the API.
5. Trigger **Scan** for that source.
6. Confirm the jobs view shows normalized roles and the source health view shows the attempt.
7. Trigger the same scan again and confirm the job count does not increase for unchanged jobs.
8. Trigger **Scan all** and confirm one source failure does not hide successful source results.

## Contract checks

```powershell
Invoke-RestMethod http://localhost:5000/api/sources
Invoke-RestMethod http://localhost:5000/api/jobs
Invoke-RestMethod http://localhost:5000/api/dashboard
```

The expected response shapes are documented in [greenhouse-source.md](contracts/greenhouse-source.md)
and the source-independent entities are documented in [data-model.md](data-model.md).

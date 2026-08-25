import { Router, type IRouter } from "express";
import {
  CreateCompanyBody, UpdateCompanyBody, CreateSourceBody, UpdateSourceBody,
  UpdateProfileBody, UpdateMatchingConfigurationBody, ListJobsQueryParams,
} from "@workspace/api-zod";
import { companies, sources, jobs, profile, matching, notifications, companyFor, createId, initials, scanSources, refreshCounts } from "../lib/job-radar-store";

const router: IRouter = Router();
const parseBody = <T>(schema: { parse: (value: unknown) => T }, value: unknown) => schema.parse(value);
const error = (res: any, message: string, status = 400) => res.status(status).json({ error: message });

router.get("/dashboard", (_req, res) => {
  res.json({
    stats: {
      companies: companies.filter((company) => company.enabled).length,
      activeSources: sources.filter((source) => source.enabled).length,
      jobs: jobs.length, newJobs: jobs.filter((job) => Date.now() - Date.parse(job.firstSeenAt) < 86400000).length,
      matchedJobs: jobs.filter((job) => job.isMatch).length, notifiedJobs: jobs.filter((job) => job.notified).length,
      failedSources: sources.filter((source) => source.status === "failed" || source.status === "warning").length,
    },
    recentMatches: jobs.filter((job) => job.isMatch).sort((a, b) => b.score - a.score).slice(0, 5),
    sourceHealth: sources,
  });
});
router.get("/companies", (_req, res) => res.json(companies));
router.post("/companies", (req, res) => {
  try {
    const body = parseBody(CreateCompanyBody, req.body);
    const company = { id: createId("company"), name: body.name, domain: body.domain, initials: initials(body.name), color: "#5B5CE2", enabled: true, sourceCount: 0, jobCount: 0, createdAt: new Date().toISOString() };
    companies.push(company); return res.status(201).json(company);
  } catch { return error(res, "Company name and domain are required."); }
});
router.patch("/companies/:id", (req, res) => {
  const company = companyFor(req.params.id); if (!company) return error(res, "Company not found.", 404);
  try { Object.assign(company, parseBody(UpdateCompanyBody, req.body)); refreshCounts(); return res.json(company); } catch { return error(res, "Invalid company update."); }
});
router.delete("/companies/:id", (req, res) => {
  const index = companies.findIndex((company) => company.id === req.params.id); if (index < 0) return error(res, "Company not found.", 404);
  companies.splice(index, 1); refreshCounts(); return res.status(204).send();
});
router.get("/sources", (_req, res) => res.json(sources));
router.post("/sources", (req, res) => {
  try {
    const body = parseBody(CreateSourceBody, req.body); const company = companyFor(body.companyId); if (!company) return error(res, "Company not found.", 404);
    const source = { id: createId("source"), companyId: body.companyId, companyName: company.name, name: body.name, type: body.type, url: body.url, enabled: true, status: "never_run" as const, lastFetch: "Never", jobsFetched: 0, failureCount: 0, lastError: null };
    sources.push(source); refreshCounts(); return res.status(201).json(source);
  } catch { return error(res, "Company, source name, type, and URL are required."); }
});
router.patch("/sources/:id", (req, res) => {
  const source = sources.find((item) => item.id === req.params.id); if (!source) return error(res, "Source not found.", 404);
  try { Object.assign(source, parseBody(UpdateSourceBody, req.body)); return res.json(source); } catch { return error(res, "Invalid source update."); }
});
router.delete("/sources/:id", (req, res) => { const index = sources.findIndex((source) => source.id === req.params.id); if (index < 0) return error(res, "Source not found.", 404); sources.splice(index, 1); refreshCounts(); return res.status(204).send(); });
router.post("/sources/:id/scan", (req, res) => res.json(scanSources([req.params.id])));
router.get("/jobs", (req, res) => {
  const query = parseBody(ListJobsQueryParams, req.query); let result = [...jobs];
  if (query.search) { const term = query.search.toLowerCase(); result = result.filter((job) => `${job.title} ${job.company} ${job.description}`.toLowerCase().includes(term)); }
  if (query.location) result = result.filter((job) => job.location.toLowerCase().includes(query.location!.toLowerCase()));
  if (query.workplaceType) result = result.filter((job) => job.workplaceType === query.workplaceType);
  if (query.status === "matched") result = result.filter((job) => job.isMatch);
  if (query.status === "notified") result = result.filter((job) => job.notified);
  if (query.status === "new") result = result.filter((job) => Date.now() - Date.parse(job.firstSeenAt) < 86400000);
  return res.json(result);
});
router.get("/jobs/:id", (req, res) => { const job = jobs.find((item) => item.id === req.params.id); return job ? res.json(job) : error(res, "Job not found.", 404); });
router.get("/profile", (_req, res) => res.json(profile));
router.put("/profile", (req, res) => { try { Object.assign(profile, parseBody(UpdateProfileBody, req.body)); return res.json(profile); } catch { return error(res, "Invalid candidate profile."); } });
router.get("/matching", (_req, res) => res.json(matching));
router.put("/matching", (req, res) => { try { const next = parseBody(UpdateMatchingConfigurationBody, req.body); if (Object.values(next).reduce((sum, value) => sum + value, 0) - next.threshold !== 100) return error(res, "Scoring weights must total 100."); Object.assign(matching, next); return res.json(matching); } catch { return error(res, "Invalid matching configuration."); } });
router.get("/notifications", (_req, res) => res.json(notifications));
router.post("/scheduler/scan", (_req, res) => res.json(scanSources(sources.map((source) => source.id))));

export default router;
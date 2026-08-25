import { randomUUID } from "node:crypto";

export type Company = {
  id: string; name: string; domain: string; initials: string; color: string;
  enabled: boolean; sourceCount: number; jobCount: number; createdAt: string;
};
export type Source = {
  id: string; companyId: string; companyName: string; name: string;
  type: "GREENHOUSE_API" | "LEVER_API" | "STRUCTURED_HTML" | "GENERIC_HTML";
  url: string; enabled: boolean; status: "healthy" | "warning" | "failed" | "never_run";
  lastFetch: string; jobsFetched: number; failureCount: number; lastError: string | null;
};
export type Job = {
  id: string; companyId: string; sourceId: string; company: string; title: string;
  description: string; location: string; workplaceType: "Remote" | "Hybrid" | "On-site" | "Unknown";
  department: string; employmentType: string; postedDate: string; firstSeenAt: string;
  applicationUrl: string; sourceUrl: string; score: number; isMatch: boolean; notified: boolean;
  matchedSkills: string[]; missingSkills: string[];
  breakdown: { role: number; skills: number; experience: number; location: number; aiRelevance: number; freshness: number };
};
export type Profile = {
  id: string; roles: string[]; skills: string[]; technologies: string[]; minYears: number; maxYears: number;
  locations: string[]; workplacePreference: "Remote" | "Hybrid" | "On-site" | "Any";
  includeKeywords: string[]; excludeKeywords: string[]; email: string;
};
export type Matching = { threshold: number; roleWeight: number; skillsWeight: number; experienceWeight: number; locationWeight: number; aiWeight: number; freshnessWeight: number };
export type Notification = { id: string; jobId: string; jobTitle: string; company: string; score: number; type: string; sentAt: string; status: "sent" | "failed" | "pending"; error: string | null };

const now = new Date();
const hoursAgo = (hours: number) => new Date(now.getTime() - hours * 3600000).toISOString();
export const initials = (name: string) => name.split(/\s+/).map((part) => part[0]).join("").slice(0, 2).toUpperCase();

export const companies: Company[] = [
  { id: "company-1", name: "Microsoft", domain: "microsoft.com", initials: "MS", color: "#5B5CE2", enabled: true, sourceCount: 1, jobCount: 34, createdAt: hoursAgo(720) },
  { id: "company-2", name: "Atlassian", domain: "atlassian.com", initials: "AT", color: "#1D78D5", enabled: true, sourceCount: 1, jobCount: 19, createdAt: hoursAgo(680) },
  { id: "company-3", name: "Razorpay", domain: "razorpay.com", initials: "RZ", color: "#25A17A", enabled: true, sourceCount: 1, jobCount: 12, createdAt: hoursAgo(500) },
  { id: "company-4", name: "Miro", domain: "miro.com", initials: "MI", color: "#F0A12B", enabled: false, sourceCount: 1, jobCount: 8, createdAt: hoursAgo(400) },
];
export const sources: Source[] = [
  { id: "source-1", companyId: "company-1", companyName: "Microsoft", name: "Microsoft Careers", type: "GREENHOUSE_API", url: "https://careers.microsoft.com/search", enabled: true, status: "healthy", lastFetch: hoursAgo(1), jobsFetched: 34, failureCount: 0, lastError: null },
  { id: "source-2", companyId: "company-2", companyName: "Atlassian", name: "Atlassian Jobs", type: "LEVER_API", url: "https://www.atlassian.com/company/careers", enabled: true, status: "healthy", lastFetch: hoursAgo(2), jobsFetched: 19, failureCount: 0, lastError: null },
  { id: "source-3", companyId: "company-3", companyName: "Razorpay", name: "Razorpay Careers", type: "STRUCTURED_HTML", url: "https://razorpay.com/jobs", enabled: true, status: "warning", lastFetch: hoursAgo(25), jobsFetched: 12, failureCount: 1, lastError: "Request timed out after 10 seconds on the previous attempt" },
  { id: "source-4", companyId: "company-4", companyName: "Miro", name: "Miro Careers", type: "GENERIC_HTML", url: "https://miro.com/careers", enabled: false, status: "never_run", lastFetch: "Never", jobsFetched: 8, failureCount: 0, lastError: null },
];
export const profile: Profile = {
  id: "profile-1", roles: ["Full Stack Developer", "Software Engineer", "AI Engineer"],
  skills: ["React", "TypeScript", "C#", "SQL"], technologies: ["Azure", "Semantic Kernel", "Azure OpenAI"],
  minYears: 2, maxYears: 6, locations: ["Pune", "Mumbai", "Bangalore", "Remote"], workplacePreference: "Any",
  includeKeywords: ["AI", "GenAI", "LLM", "platform"], excludeKeywords: ["senior manager", "sales"], email: "omkar@example.com",
};
export const matching: Matching = { threshold: 70, roleWeight: 30, skillsWeight: 30, experienceWeight: 15, locationWeight: 10, aiWeight: 10, freshnessWeight: 5 };
export const jobs: Job[] = [
  { id: "job-1", companyId: "company-1", sourceId: "source-1", company: "Microsoft", title: "Full Stack Software Engineer", description: "Build modern cloud services and delightful web experiences with React, TypeScript, Azure, and .NET. Work with product teams on AI-powered developer tools.", location: "Bangalore, India", workplaceType: "Hybrid", department: "Engineering", employmentType: "Full-time", postedDate: hoursAgo(9), firstSeenAt: hoursAgo(8), applicationUrl: "https://careers.microsoft.com/job/1001", sourceUrl: "https://careers.microsoft.com/search", score: 92, isMatch: true, notified: true, matchedSkills: ["React", "TypeScript", "Azure", "C#", "AI"], missingSkills: ["Semantic Kernel"], breakdown: { role: 29, skills: 28, experience: 14, location: 9, aiRelevance: 9, freshness: 3 } },
  { id: "job-2", companyId: "company-2", sourceId: "source-2", company: "Atlassian", title: "Software Engineer, AI Platform", description: "Join the platform group building reliable AI capabilities. You will ship TypeScript services, collaborate across teams, and shape our developer experience.", location: "Remote - India", workplaceType: "Remote", department: "Platform", employmentType: "Full-time", postedDate: hoursAgo(18), firstSeenAt: hoursAgo(17), applicationUrl: "https://jobs.lever.co/atlassian/1002", sourceUrl: "https://www.atlassian.com/company/careers", score: 88, isMatch: true, notified: true, matchedSkills: ["TypeScript", "AI", "platform"], missingSkills: ["React", "Azure"], breakdown: { role: 27, skills: 25, experience: 14, location: 10, aiRelevance: 9, freshness: 3 } },
  { id: "job-3", companyId: "company-3", sourceId: "source-3", company: "Razorpay", title: "Backend Engineer - Payments", description: "Design scalable payment systems and APIs. Experience with distributed systems and SQL preferred.", location: "Bangalore, India", workplaceType: "On-site", department: "Engineering", employmentType: "Full-time", postedDate: hoursAgo(31), firstSeenAt: hoursAgo(30), applicationUrl: "https://razorpay.com/jobs/1003", sourceUrl: "https://razorpay.com/jobs", score: 68, isMatch: false, notified: false, matchedSkills: ["SQL"], missingSkills: ["React", "TypeScript", "Azure"], breakdown: { role: 18, skills: 16, experience: 14, location: 8, aiRelevance: 3, freshness: 4 } },
  { id: "job-4", companyId: "company-1", sourceId: "source-1", company: "Microsoft", title: "AI Engineer, Applied Intelligence", description: "Develop agentic AI experiences using Azure OpenAI, semantic retrieval, and production machine learning systems.", location: "Pune, India", workplaceType: "Hybrid", department: "Applied Intelligence", employmentType: "Full-time", postedDate: hoursAgo(42), firstSeenAt: hoursAgo(40), applicationUrl: "https://careers.microsoft.com/job/1004", sourceUrl: "https://careers.microsoft.com/search", score: 84, isMatch: true, notified: false, matchedSkills: ["Azure OpenAI", "AI", "Semantic Kernel"], missingSkills: ["React"], breakdown: { role: 24, skills: 26, experience: 13, location: 9, aiRelevance: 10, freshness: 2 } },
  { id: "job-5", companyId: "company-2", sourceId: "source-2", company: "Atlassian", title: "Frontend Engineer, Design Systems", description: "Create accessible component systems and web interfaces with React and TypeScript for millions of users.", location: "Mumbai, India", workplaceType: "Hybrid", department: "Frontend", employmentType: "Full-time", postedDate: hoursAgo(66), firstSeenAt: hoursAgo(64), applicationUrl: "https://jobs.lever.co/atlassian/1005", sourceUrl: "https://www.atlassian.com/company/careers", score: 79, isMatch: true, notified: false, matchedSkills: ["React", "TypeScript"], missingSkills: ["Azure", "AI"], breakdown: { role: 26, skills: 22, experience: 13, location: 8, aiRelevance: 5, freshness: 5 } },
];
export const notifications: Notification[] = [
  { id: "notification-1", jobId: "job-1", jobTitle: "Full Stack Software Engineer", company: "Microsoft", score: 92, type: "Match alert", sentAt: hoursAgo(8), status: "sent", error: null },
  { id: "notification-2", jobId: "job-2", jobTitle: "Software Engineer, AI Platform", company: "Atlassian", score: 88, type: "Match alert", sentAt: hoursAgo(17), status: "sent", error: null },
];

export function refreshCounts() {
  for (const company of companies) {
    company.sourceCount = sources.filter((source) => source.companyId === company.id).length;
    company.jobCount = jobs.filter((job) => job.companyId === company.id).length;
  }
}
export function companyFor(id: string) { return companies.find((company) => company.id === id); }
export function createId(prefix: string) { return `${prefix}-${randomUUID().slice(0, 8)}`; }
export function scanSources(sourceIds: string[]) {
  const selected = sources.filter((source) => sourceIds.includes(source.id) && source.enabled);
  for (const source of selected) { source.lastFetch = new Date().toISOString(); source.status = "healthy"; source.lastError = null; }
  return { sourcesScanned: selected.length, jobsFetched: selected.reduce((sum, source) => sum + source.jobsFetched, 0), newJobs: selected.length ? 2 : 0, matchedJobs: selected.length ? 2 : 0, notificationsSent: 0 };
}
refreshCounts();
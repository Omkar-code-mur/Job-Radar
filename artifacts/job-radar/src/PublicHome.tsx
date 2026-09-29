import { useEffect, useState } from 'react';
import { ArrowUpRight, BriefcaseBusiness, LogIn, RefreshCw, Search, ShieldCheck, Sparkles } from 'lucide-react';

type PublicJob = {
  id: string;
  company: string;
  title: string;
  location: string;
  workplaceType: string;
  postedDate: string;
  firstSeenAt: string;
  applicationUrl: string;
};

const API_BASE = ((import.meta.env.VITE_API_URL as string | undefined)?.trim() || 'https://job-radar-nfgv.onrender.com').replace(/\/+$/, '');

function relative(value?: string) {
  if (!value) return 'Recently added';
  const time = new Date(value).getTime();
  if (Number.isNaN(time)) return 'Recently added';
  const diff = Math.max(0, Date.now() - time);
  if (diff < 60 * 60 * 1000) return `${Math.max(1, Math.round(diff / 60000))}m ago`;
  if (diff < 24 * 60 * 60 * 1000) return `${Math.round(diff / 3600000)}h ago`;
  return `${Math.round(diff / 86400000)}d ago`;
}

function initials(value: string) {
  return value.split(/\s+/).map(part => part[0]).join('').slice(0, 2).toUpperCase();
}

async function loadPublicJobs(search: string) {
  const params = new URLSearchParams({ limit: '30' });
  if (search.trim()) params.set('search', search.trim());
  const response = await fetch(`${API_BASE}/api/public/jobs?${params.toString()}`, {
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) throw new Error('Could not load jobs.');
  return response.json() as Promise<PublicJob[]>;
}

export default function PublicHome({ onRequireLogin }: { onRequireLogin: () => void }) {
  const [jobs, setJobs] = useState<PublicJob[]>([]);
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = () => {
    setLoading(true);
    setError('');
    loadPublicJobs(query)
      .then(setJobs)
      .catch(() => setError('Jobs are temporarily unavailable. Please try again.'))
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, []);

  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="sticky top-0 z-20 border-b bg-card/95 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4 sm:px-8">
          <div className="flex items-center gap-2 font-bold">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-primary-foreground">JR</span>
            <span>Job Radar</span>
          </div>
          <button type="button" onClick={onRequireLogin} className="btn btn-primary">
            <LogIn size={15} /> Sign in
          </button>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-8 sm:py-12">
        <section className="mb-9">
          <div className="max-w-3xl">
            <p className="mono text-[10px] font-bold uppercase tracking-[.2em] text-muted-foreground">Public job feed</p>
            <h1 className="mt-2 text-3xl font-bold tracking-tight sm:text-5xl">Find your next role without signing in.</h1>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
              Browse the latest jobs collected from public company career pages. You only need an account when you want Job Radar to save or personalize something for you.
            </p>
          </div>

          <div className="mt-6 flex flex-col gap-3 sm:flex-row">
            <div className="relative flex-1">
              <Search size={17} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <input
                className="field pl-10"
                value={query}
                onChange={event => setQuery(event.target.value)}
                onKeyDown={event => { if (event.key === 'Enter') load(); }}
                placeholder="Search role, company or location"
                aria-label="Search public jobs"
              />
            </div>
            <button type="button" className="btn btn-primary" onClick={load} disabled={loading}>
              <Search size={15} /> Search
            </button>
          </div>
        </section>

        <div className="mb-4 flex items-center justify-between">
          <div>
            <h2 className="text-lg font-bold">Latest jobs</h2>
            <p className="mt-1 text-xs text-muted-foreground">Newest jobs added to Job Radar appear first.</p>
          </div>
          <button type="button" className="btn btn-ghost" onClick={load} disabled={loading} aria-label="Refresh jobs">
            <RefreshCw size={15} className={loading ? 'animate-spin' : ''} />
          </button>
        </div>

        {loading ? (
          <div className="grid gap-3 sm:grid-cols-2">
            {Array.from({ length: 6 }).map((_, index) => <div key={index} className="h-40 animate-pulse rounded-xl border bg-card" />)}
          </div>
        ) : error ? (
          <div className="card p-8 text-center">
            <p className="font-semibold">{error}</p>
            <button type="button" className="btn btn-primary mt-4" onClick={load}>Try again</button>
          </div>
        ) : !jobs.length ? (
          <div className="card p-10 text-center">
            <BriefcaseBusiness className="mx-auto text-muted-foreground" size={24} />
            <h3 className="mt-3 font-bold">No jobs found</h3>
            <p className="mt-1 text-sm text-muted-foreground">Try another search or check back after the next source scan.</p>
          </div>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {jobs.map(job => (
              <article key={job.id} className="card flex min-h-44 flex-col p-5 transition hover:-translate-y-0.5 hover:shadow-md">
                <div className="flex gap-3">
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary text-xs font-bold text-primary-foreground">
                    {initials(job.company)}
                  </div>
                  <div className="min-w-0 flex-1">
                    <h3 className="font-bold leading-5">{job.title}</h3>
                    <p className="mt-1 text-sm text-muted-foreground">{job.company}</p>
                  </div>
                </div>
                <div className="mt-4 flex flex-wrap gap-2 text-xs text-muted-foreground">
                  <span className="rounded-md bg-secondary px-2 py-1">{job.location || 'Location flexible'}</span>
                  {job.workplaceType && job.workplaceType !== 'Unknown' && <span className="rounded-md bg-secondary px-2 py-1">{job.workplaceType}</span>}
                  <span className="rounded-md bg-secondary px-2 py-1">Added {relative(job.firstSeenAt)}</span>
                </div>
                <div className="mt-auto flex items-center justify-between gap-3 pt-5">
                  <button type="button" onClick={onRequireLogin} className="text-xs font-semibold text-muted-foreground hover:text-foreground">
                    <Sparkles size={13} className="mr-1 inline" /> Save to Job Radar
                  </button>
                  <a
                    href={job.applicationUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="btn btn-primary"
                  >
                    Apply <ArrowUpRight size={14} />
                  </a>
                </div>
              </article>
            ))}
          </div>
        )}

        <section className="mt-10 grid gap-3 sm:grid-cols-3">
          {[
            ['Browse first', 'No account is required to view public job listings.'],
            ['Apply directly', 'Application buttons take you to the company or recruiting platform.'],
            ['Sign in when useful', 'Use an account for saved jobs, applications, matching and other private features.'],
          ].map(([title, detail]) => (
            <div key={title} className="rounded-xl border bg-card p-4">
              <ShieldCheck size={17} className="text-primary" />
              <div className="mt-2 text-sm font-bold">{title}</div>
              <p className="mt-1 text-xs leading-5 text-muted-foreground">{detail}</p>
            </div>
          ))}
        </section>
      </main>
    </div>
  );
}

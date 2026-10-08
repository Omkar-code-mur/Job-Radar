import { useEffect, useState } from 'react';
import { ArrowDownUp, ArrowUpRight, Bell, Bookmark, BriefcaseBusiness, CheckCircle2, ClipboardList, LogIn, RefreshCw, Search, ShieldCheck, Sparkles, UserRound } from 'lucide-react';

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
  const [sort, setSort] = useState('newest');
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

  const sortedJobs = jobs.slice().sort((a, b) => {
    if (sort === 'oldest') return new Date(a.firstSeenAt).getTime() - new Date(b.firstSeenAt).getTime();
    if (sort === 'posted') return new Date(b.postedDate).getTime() - new Date(a.postedDate).getTime();
    if (sort === 'company') return a.company.localeCompare(b.company) || new Date(b.firstSeenAt).getTime() - new Date(a.firstSeenAt).getTime();
    if (sort === 'title') return a.title.localeCompare(b.title) || new Date(b.firstSeenAt).getTime() - new Date(a.firstSeenAt).getTime();
    return new Date(b.firstSeenAt).getTime() - new Date(a.firstSeenAt).getTime();
  });

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
            <p className="mono text-[10px] font-bold uppercase tracking-[.2em] text-muted-foreground">Job Radar</p>
            <h1 className="mt-2 text-3xl font-bold tracking-tight sm:text-5xl">A simpler way to find and manage your next job.</h1>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
              Search opportunities from company career pages, keep the ones that matter to you in one place, and use Job Radar to organize your job search from discovery to application.
            </p>
            <div className="mt-5 flex flex-wrap items-center gap-x-5 gap-y-2 text-xs font-medium text-muted-foreground">
              <span>Fresh job listings</span>
              <span>Personalized matching</span>
              <span>Application tracking</span>
              <span>AI-powered insights</span>
            </div>
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

        <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-lg font-bold">Latest jobs</h2>
            <p className="mt-1 text-xs text-muted-foreground">Newest jobs added to Job Radar appear first.</p>
          </div>
          <div className="flex items-center gap-2">
            <ArrowDownUp size={14} className="text-muted-foreground" aria-hidden="true" />
            <label className="sr-only" htmlFor="public-job-sort">Sort jobs</label>
            <select id="public-job-sort" value={sort} onChange={event => setSort(event.target.value)} className="field py-2 text-xs">
              <option value="newest">Newest added</option>
              <option value="oldest">Oldest added</option>
              <option value="posted">Recently posted</option>
              <option value="company">Company A–Z</option>
              <option value="title">Job title A–Z</option>
            </select>
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
            {sortedJobs.map(job => (
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

        <section className="mt-14">
          <div className="max-w-2xl">
            <p className="mono text-[10px] font-bold uppercase tracking-[.2em] text-muted-foreground">Built for the whole job search</p>
            <h2 className="mt-2 text-2xl font-bold tracking-tight sm:text-3xl">More than a job board.</h2>
            <p className="mt-2 text-sm leading-6 text-muted-foreground">
              Create a free account when you're ready and turn the job search into a workspace built around your goals.
            </p>
          </div>

          <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {[
              [Search, 'Discover jobs', 'Search and explore opportunities collected from company career pages in one place.'],
              [Bookmark, 'Save and shortlist', 'Keep interesting roles close by so you can come back to them when you are ready.'],
              [UserRound, 'Build your profile', 'Add your roles, skills, experience, locations and preferences once.'],
              [Sparkles, 'Personalized matching', 'See how opportunities align with your profile and the skills you care about.'],
              [ClipboardList, 'Track applications', 'Keep your applications, statuses, notes and follow-ups organized in one workspace.'],
              [Bell, 'Stay on top of new roles', 'Get notified about relevant opportunities and important updates in your job search.'],
            ].map(([Icon, title, detail]) => (
              <div key={title as string} className="rounded-xl border bg-card p-5">
                <Icon size={19} className="text-primary" />
                <h3 className="mt-3 text-sm font-bold">{title as string}</h3>
                <p className="mt-1.5 text-xs leading-5 text-muted-foreground">{detail as string}</p>
              </div>
            ))}
          </div>

          <div className="mt-6 rounded-xl border bg-secondary/40 p-5 sm:flex sm:items-center sm:justify-between sm:gap-6">
            <div>
              <h3 className="font-bold">Ready to make your search more organized?</h3>
              <p className="mt-1 text-sm text-muted-foreground">Create your Job Radar account and keep your search, matches and applications together.</p>
            </div>
            <button type="button" onClick={onRequireLogin} className="btn btn-primary mt-4 shrink-0 sm:mt-0">
              <LogIn size={15} /> Get started
            </button>
          </div>
        </section>

        <section className="mt-8 flex flex-wrap items-center justify-center gap-x-5 gap-y-2 border-t pt-6 text-xs text-muted-foreground">
          <span className="inline-flex items-center gap-1.5"><ShieldCheck size={14} /> Public job browsing</span>
          <span className="inline-flex items-center gap-1.5"><ArrowUpRight size={14} /> Apply directly through the original listing</span>
          <span className="inline-flex items-center gap-1.5"><CheckCircle2 size={14} /> Your private workspace stays behind your account</span>
        </section>
      </main>
    </div>
  );
}

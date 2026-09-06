import { useEffect, useState } from 'react';

type Job = { id:string; company:string; title:string; location:string; workplaceType:string; postedDate:string; applicationUrl:string; score:number; isMatch:boolean; matchedSkills:string[]; missingSkills:string[] };
type AiAnalysis = { verdict:'STRONG_FIT'|'POSSIBLE_FIT'|'WEAK_FIT'; fitScore:number; summary:string; strengths:string[]; gaps:string[]; concerns:string[]; interviewFocus:string[]; nextAction:string };

const SESSION_KEY='jobradar.supabase.session';
function token(){try{return (JSON.parse(sessionStorage.getItem(SESSION_KEY)||'{}') as {access_token?:string}).access_token||''}catch{return ''}}
async function api<T>(path:string,init:RequestInit={}):Promise<T>{const headers=new Headers(init.headers);headers.set('Authorization',`Bearer ${token()}`);headers.set('Content-Type','application/json');const r=await fetch(`/api${path}`,{...init,headers});if(!r.ok)throw new Error(await r.text()||`Request failed (${r.status})`);return r.status===204?undefined as T:await r.json()}
function Card({children,className='' }:{children:React.ReactNode;className?:string}){return <div className={`rounded-xl border bg-card p-5 shadow-sm ${className}`}>{children}</div>}
function Button({children,onClick,primary=false,disabled=false}:{children:React.ReactNode;onClick?:()=>void;primary?:boolean;disabled?:boolean}){return <button disabled={disabled} onClick={onClick} className={`inline-flex items-center justify-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold transition ${primary?'bg-primary text-primary-foreground hover:opacity-90':'border bg-background hover:bg-muted'} disabled:opacity-50`}>{children}</button>}

export default function JobDetails({jobId,onBack}:{jobId:string;onBack:()=>void}){
 const[job,setJob]=useState<Job|null>(null);const[analysis,setAnalysis]=useState<AiAnalysis|null>(null);const[loading,setLoading]=useState(true);const[analyzing,setAnalyzing]=useState(false);const[error,setError]=useState('');
 useEffect(()=>{setLoading(true);setError('');api<Job>(`/jobs/${jobId}`).then(setJob).catch(e=>setError(e.message)).finally(()=>setLoading(false))},[jobId]);
 const analyze=async()=>{setAnalyzing(true);setError('');try{setAnalysis(await api<AiAnalysis>(`/jobs/${jobId}/ai-analysis`,{method:'POST'}))}catch(e){setError(e instanceof Error?e.message:'AI analysis failed')}finally{setAnalyzing(false)}};
 if(loading)return <div className="py-12 text-sm text-muted-foreground">Loading job…</div>;
 if(!job)return <div className="space-y-4"><Button onClick={onBack}>← Back</Button><Card>{error||'Job not found.'}</Card></div>;
 return <div className="max-w-4xl space-y-5">
  <Button onClick={onBack}>← Back to jobs</Button>
  <Card><div className="flex flex-col gap-5 sm:flex-row sm:items-start sm:justify-between"><div><p className="text-sm text-muted-foreground">{job.company}</p><h1 className="mt-1 text-3xl font-bold tracking-tight">{job.title}</h1><p className="mt-2 text-sm text-muted-foreground">{job.location||'Flexible'} · {job.workplaceType||'—'}</p></div><div className="flex gap-2"><span className="rounded-md bg-secondary px-3 py-2 text-sm font-bold">{job.score}% match</span><Button primary onClick={()=>window.open(job.applicationUrl,'_blank','noopener,noreferrer')}>Apply ↗</Button></div></div>
   <div className="mt-6 grid gap-5 sm:grid-cols-2"><div><h2 className="font-bold">Why it matches</h2><div className="mt-2 flex flex-wrap gap-2">{job.matchedSkills?.map(x=><span key={x} className="rounded-md bg-secondary px-2 py-1 text-xs">{x}</span>)}{!job.matchedSkills?.length&&<span className="text-sm text-muted-foreground">No matched skills recorded.</span>}</div></div><div><h2 className="font-bold">Missing skills</h2><div className="mt-2 flex flex-wrap gap-2">{job.missingSkills?.map(x=><span key={x} className="rounded-md border px-2 py-1 text-xs">{x}</span>)}{!job.missingSkills?.length&&<span className="text-sm text-muted-foreground">None recorded.</span>}</div></div></div>
  </Card>
  <Card><div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between"><div><h2 className="text-lg font-bold">AI job intelligence</h2><p className="mt-1 text-sm text-muted-foreground">Analyze this job only when you choose to. Nothing runs automatically.</p></div><Button primary onClick={analyze} disabled={analyzing}>{analyzing?'Analyzing…':'Analyze with AI'}</Button></div>
   {error&&<div className="mt-4 rounded-lg border px-3 py-2 text-sm">{error}</div>}
   {analysis&&<div className="mt-6 space-y-5"><div className="flex flex-wrap items-center gap-3"><span className="rounded-md bg-secondary px-3 py-1.5 text-sm font-bold">{analysis.verdict.replace('_',' ')}</span><span className="text-2xl font-bold">{analysis.fitScore}% AI fit</span></div><p className="text-sm leading-6">{analysis.summary}</p><div className="grid gap-5 sm:grid-cols-2">{[['Strengths',analysis.strengths],['Gaps',analysis.gaps],['Concerns',analysis.concerns],['Interview focus',analysis.interviewFocus]].map(([title,items])=><div key={String(title)}><h3 className="font-bold">{title}</h3><ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-muted-foreground">{(items as string[]).map(x=><li key={x}>{x}</li>)}</ul></div>)}</div><div className="rounded-lg border p-4"><div className="text-xs font-bold uppercase tracking-[.16em] text-muted-foreground">Recommended next action</div><p className="mt-2 text-sm font-medium">{analysis.nextAction}</p></div></div>}
  </Card>
 </div>;
}

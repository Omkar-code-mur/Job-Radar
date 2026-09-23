import { ChangeEvent, useMemo, useState } from 'react';
import { Mail, Send, Upload, Users } from 'lucide-react';
import { DEFAULT_EMAIL_TEMPLATES, chunk, parseContacts, renderTemplate, type BulkEmailContact } from './bulkEmail';

const SESSION_KEY='jobradar.supabase.session';
function token(){try{return (JSON.parse(sessionStorage.getItem(SESSION_KEY)||'{}') as {access_token?:string}).access_token||''}catch{return ''}}

export default function BulkEmailPage() {
  const [raw, setRaw] = useState('');
  const [templateId, setTemplateId] = useState(DEFAULT_EMAIL_TEMPLATES[0].id);
  const [subject, setSubject] = useState(DEFAULT_EMAIL_TEMPLATES[0].subject);
  const [body, setBody] = useState(DEFAULT_EMAIL_TEMPLATES[0].body);
  const [batchSize, setBatchSize] = useState(20);
  const [batchIndex, setBatchIndex] = useState(0);
  const [status, setStatus] = useState('');
  const [busy, setBusy] = useState(false);

  const parsed = useMemo(() => {
    if (!raw.trim()) return { contacts: [] as BulkEmailContact[], error: '' };
    try { return { contacts: parseContacts(raw), error: '' }; }
    catch (e) { return { contacts: [] as BulkEmailContact[], error: e instanceof Error ? e.message : 'Invalid contact data.' }; }
  }, [raw]);

  const batches = useMemo(() => chunk(parsed.contacts, batchSize), [parsed.contacts, batchSize]);
  const currentBatch = batches[batchIndex] || [];
  const preview = currentBatch.slice(0,3).map((contact) => renderTemplate({ id:'preview', name:'Preview', subject, body }, contact));

  function chooseTemplate(id: string) {
    const t = DEFAULT_EMAIL_TEMPLATES.find(x => x.id === id) || DEFAULT_EMAIL_TEMPLATES[0];
    setTemplateId(t.id); setSubject(t.subject); setBody(t.body);
  }

  async function importCsv(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]; if (!file) return;
    setRaw(await file.text()); setBatchIndex(0);
  }

  async function sendBatch() {
    if (!currentBatch.length) return;
    setBusy(true); setStatus('');
    try {
      const r = await fetch('/api/bulk-email/send', {
        method: 'POST',
        headers: { 'Content-Type':'application/json', 'Authorization':'Bearer ' + token() },
        body: JSON.stringify({ contacts: currentBatch, subject, body }),
      });
      const data = await r.json().catch(()=>({}));
      if (!r.ok) throw new Error(data?.detail || data?.error || 'Send failed.');
      setStatus('Sent ' + data.sent + ' email(s). ' + data.failed + ' failed.');
      if (batchIndex < batches.length - 1) setBatchIndex(i => i + 1);
    } catch (e) { setStatus(e instanceof Error ? e.message : 'Send failed.'); }
    finally { setBusy(false); }
  }

  return <div className="rise">
    <div className="mb-7"><div className="mono mb-2 text-[10px] font-bold uppercase tracking-[.2em] text-muted-foreground">Outreach / bulk email</div><h1 className="text-3xl font-bold tracking-tight">Bulk email outreach</h1><p className="mt-1 text-sm text-muted-foreground">Paste contacts or upload CSV, personalize a template, preview, then send one controlled batch at a time.</p></div>

    <div className="grid gap-6 xl:grid-cols-[1.1fr_.9fr]">
      <section className="card p-5"><div className="flex items-center justify-between"><div><h2 className="font-bold">1. Add contacts</h2><p className="mt-1 text-xs text-muted-foreground">Columns: Name, Email, Role, Company. Tab-separated paste or CSV both work.</p></div><div className="flex items-center gap-2 text-xs text-muted-foreground"><Users size={15}/>{parsed.contacts.length}</div></div>
        <textarea className="field mt-4 min-h-72 font-mono text-xs" value={raw} onChange={e=>{setRaw(e.target.value);setBatchIndex(0)}} placeholder={'Akanksha Puri\takanksha.puri@example.com\tAssociate Director HR\tSourceFuse Technologies'} />
        <div className="mt-3 flex items-center gap-3"><label className="btn btn-ghost cursor-pointer"><Upload size={15}/>Upload CSV<input className="hidden" type="file" accept=".csv,text/csv" onChange={importCsv}/></label><span className="text-xs text-muted-foreground">Excel: export/save as CSV first.</span></div>
        {parsed.error && <p className="mt-3 rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-xs text-destructive">{parsed.error}</p>}
      </section>

      <section className="card p-5"><h2 className="font-bold">2. Template</h2>
        <select className="field mt-3" value={templateId} onChange={e=>chooseTemplate(e.target.value)}>{DEFAULT_EMAIL_TEMPLATES.map(t=><option key={t.id} value={t.id}>{t.name}</option>)}</select>
        <label className="label mt-4">Subject</label><input className="field" value={subject} onChange={e=>setSubject(e.target.value)} />
        <label className="label mt-4">Body</label><textarea className="field min-h-64" value={body} onChange={e=>setBody(e.target.value)} />
        <p className="mt-2 text-xs text-muted-foreground">Variables: {'{{firstName}}'}, {'{{name}}'}, {'{{company}}'}, {'{{role}}'}</p>
      </section>
    </div>

    <div className="mt-6 grid gap-6 xl:grid-cols-[.7fr_1.3fr]">
      <section className="card p-5"><h2 className="font-bold">3. Batch controls</h2><label className="label mt-4">Batch size</label>
        <select className="field" value={batchSize} onChange={e=>{setBatchSize(Number(e.target.value));setBatchIndex(0)}}>{[10,20,30].map(n=><option key={n} value={n}>{n} recipients</option>)}</select>
        <div className="mt-4 rounded-xl bg-secondary p-4 text-sm"><div className="font-bold">Batch {batches.length ? batchIndex+1 : 0} of {batches.length}</div><div className="mt-1 text-xs text-muted-foreground">{currentBatch.length} recipient(s) in the current batch.</div></div>
        <div className="mt-3 flex gap-2"><button className="btn btn-ghost flex-1" disabled={batchIndex===0} onClick={()=>setBatchIndex(i=>Math.max(0,i-1))}>Previous</button><button className="btn btn-ghost flex-1" disabled={batchIndex>=batches.length-1} onClick={()=>setBatchIndex(i=>Math.min(batches.length-1,i+1))}>Next</button></div>
        <button className="btn btn-primary mt-4 w-full" disabled={busy||!currentBatch.length||!!parsed.error} onClick={sendBatch}><Send size={15}/>{busy?'Sending…':'Send current batch ('+currentBatch.length+')'}</button>
        {status && <p className="mt-3 rounded-lg border bg-background px-3 py-2 text-xs">{status}</p>}
      </section>

      <section className="card p-5"><div className="flex items-center justify-between"><div><h2 className="font-bold">4. Preview</h2><p className="mt-1 text-xs text-muted-foreground">First three emails from the current batch.</p></div><Mail size={18} className="text-muted-foreground"/></div>
        <div className="mt-4 space-y-4">{preview.map(({contact,subject:ps,body:pb})=><article key={contact.email} className="rounded-xl border bg-background p-4"><div className="text-xs text-muted-foreground">To: {contact.name} &lt;{contact.email}&gt; · {contact.company}</div><div className="mt-2 font-bold">{ps}</div><pre className="mt-3 whitespace-pre-wrap font-sans text-sm text-muted-foreground">{pb}</pre></article>)}
        {!preview.length && <div className="rounded-xl border border-dashed p-8 text-center text-sm text-muted-foreground">Add valid contacts to preview personalized emails.</div>}</div>
      </section>
    </div>
  </div>;
}
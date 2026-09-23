export type BulkEmailContact = { name: string; email: string; role?: string; company: string; };
export type EmailTemplate = { id: string; name: string; subject: string; body: string; };
export const getFirstName = (name: string) => name.trim().split(/\s+/)[0] || '';
const TOKEN_RE = /\{\{\s*(name|firstName|company|role)\s*\}\}/gi;
export function renderTemplate(template: EmailTemplate, contact: BulkEmailContact) {
  const values: Record<string,string> = { name: contact.name, firstName: getFirstName(contact.name), company: contact.company, role: contact.role || '' };
  const render = (value: string) => value.replace(TOKEN_RE, (_m, token: string) => values[token] ?? '');
  return { contact, subject: render(template.subject), body: render(template.body) };
}
export function chunk<T>(items: T[], size: number): T[][] {
  if (!Number.isInteger(size) || size < 1) throw new Error('Batch size must be a positive integer.');
  const out: T[][] = []; for (let i=0;i<items.length;i+=size) out.push(items.slice(i,i+size)); return out;
}
export function parseContacts(input: string): BulkEmailContact[] {
  const lines = input.split(/\r?\n/).map(x=>x.trim()).filter(Boolean); if (!lines.length) return [];
  const delimiter = lines[0].includes('\t') ? '\t' : ','; const start=/^name\s*[,\t]/i.test(lines[0]) ? 1 : 0;
  return lines.slice(start).map((line, i) => {
    const [name,email,role,company] = line.split(delimiter).map(x=>x.trim().replace(/^"|"$/g,''));
    if(!name||!email||!company) throw new Error(`Invalid contact on row ${i+start+1}. Expected: Name, Email, Role, Company.`);
    if(!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) throw new Error(`Invalid email on row ${i+start+1}: ${email}`);
    return {name,email,role,company};
  });
}
export const DEFAULT_EMAIL_TEMPLATES: EmailTemplate[] = [
  { id:'referral-intro', name:'HR / Talent introduction', subject:'Application inquiry — Full Stack .NET + React', body:'Hi {{firstName}},\n\nI am Omkar, a Full Stack Web Developer with 2+ years of experience in React, .NET, SQL Server and Azure AI services. I am exploring opportunities at {{company}} and wanted to ask about relevant Full Stack / Software Engineer openings.\n\nThanks,\nOmkar Kodmur' },
  { id:'short-intro', name:'Short introduction', subject:'Full Stack Developer opportunity at {{company}}', body:'Hi {{firstName}},\n\nI am exploring Full Stack opportunities at {{company}}. My background is in React, .NET, SQL Server and Azure AI services. Could you please let me know if there are relevant openings?\n\nRegards,\nOmkar Kodmur' }
];
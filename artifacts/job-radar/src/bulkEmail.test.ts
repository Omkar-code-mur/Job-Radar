import { describe, expect, it } from 'vitest';
import { DEFAULT_EMAIL_TEMPLATES, chunk, parseContacts, renderTemplate } from './bulkEmail';

describe('bulk email helpers',()=>{
 it('parses tab-separated contacts',()=>expect(parseContacts('Name\tEmail\tRole\tCompany\nA Puri\ta@example.com\tHead HR\tAcme')).toEqual([{name:'A Puri',email:'a@example.com',role:'Head HR',company:'Acme'}]));
 it('personalizes templates',()=>{const r=renderTemplate(DEFAULT_EMAIL_TEMPLATES[0],{name:'Akanksha Puri',email:'a@example.com',role:'Head HR',company:'SourceFuse'});expect(r.subject).toContain('Full Stack');expect(r.body).toContain('Akanksha');expect(r.body).toContain('SourceFuse');});
 it('creates predictable batches',()=>expect(chunk([1,2,3,4,5],2)).toEqual([[1,2],[3,4],[5]]));
});

import { useEffect, useState } from 'react';
import App from './App';
import UserApp from './UserApp';
import AdminControls from './AdminControls';

const SESSION_KEY='jobradar.supabase.session';
function token(){try{return (JSON.parse(sessionStorage.getItem(SESSION_KEY)||'{}') as {access_token?:string}).access_token||''}catch{return ''}}

type Me={role:string};
export default function RoleAwareApp(){
 const [me,setMe]=useState<Me|null>(null);const [loading,setLoading]=useState(true);
 useEffect(()=>{fetch('/api/auth/me',{headers:{Authorization:`Bearer ${token()}`}}).then(r=>r.ok?r.json():null).then(setMe).finally(()=>setLoading(false))},[]);
 if(loading)return <div className="flex min-h-screen items-center justify-center bg-background text-muted-foreground">Loading workspace…</div>;
 if(me?.role==='ADMIN')return <><App/><AdminControls/></>;
 return <UserApp/>;
}

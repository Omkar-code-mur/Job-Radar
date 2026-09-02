import { FormEvent, ReactNode, useEffect, useMemo, useState } from 'react';
import { setAuthTokenGetter } from '@workspace/api-client-react';

const SESSION_KEY = 'jobradar.supabase.session';

type AuthSession = {
  access_token: string;
  refresh_token: string;
  expires_in?: number;
  expires_at?: number;
  user?: { id: string; email?: string };
};

type AuthGateProps = { children: ReactNode };

function getSupabaseConfig() {
  const url = import.meta.env.VITE_SUPABASE_URL as string | undefined;
  const anonKey = import.meta.env.VITE_SUPABASE_ANON_KEY as string | undefined;
  return { url: url?.replace(/\/+$/, ''), anonKey };
}

function loadSession(): AuthSession | null {
  try {
    const raw = sessionStorage.getItem(SESSION_KEY);
    return raw ? (JSON.parse(raw) as AuthSession) : null;
  } catch {
    return null;
  }
}

function saveSession(session: AuthSession | null) {
  if (session) sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
  else sessionStorage.removeItem(SESSION_KEY);
}

async function supabaseAuth(path: string, body: Record<string, unknown>) {
  const { url, anonKey } = getSupabaseConfig();
  if (!url || !anonKey) {
    throw new Error('VITE_SUPABASE_URL and VITE_SUPABASE_ANON_KEY must be configured.');
  }

  const response = await fetch(`${url}/auth/v1/${path}`, {
    method: 'POST',
    headers: {
      apikey: anonKey,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  });

  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(data.error_description ?? data.msg ?? data.message ?? 'Authentication failed.');
  }
  return data as AuthSession & { user?: AuthSession['user'] };
}

async function refreshSession(session: AuthSession) {
  const refreshed = await supabaseAuth('token?grant_type=refresh_token', {
    refresh_token: session.refresh_token,
  });
  saveSession(refreshed);
  return refreshed;
}

async function verifySession(session: AuthSession) {
  const response = await fetch('/api/auth/me', {
    headers: { Authorization: `Bearer ${session.access_token}` },
  });
  return response.ok;
}

export default function AuthGate({ children }: AuthGateProps) {
  const [session, setSession] = useState<AuthSession | null>(() => loadSession());
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [mode, setMode] = useState<'sign-in' | 'sign-up'>('sign-in');
  const [busy, setBusy] = useState(false);
  const [checking, setChecking] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const tokenGetter = useMemo(() => async () => loadSession()?.access_token ?? null, []);

  useEffect(() => {
    setAuthTokenGetter(tokenGetter);
    return () => setAuthTokenGetter(null);
  }, [tokenGetter]);

  useEffect(() => {
    let active = true;

    async function check() {
      const existing = loadSession();
      if (!existing) {
        if (active) setChecking(false);
        return;
      }

      try {
        if (await verifySession(existing)) {
          if (active) setSession(existing);
          return;
        }

        const refreshed = await refreshSession(existing);
        if (active) setSession(refreshed);
      } catch {
        saveSession(null);
        if (active) setSession(null);
      } finally {
        if (active) setChecking(false);
      }
    }

    void check();
    return () => {
      active = false;
    };
  }, []);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError('');
    setMessage('');

    try {
      const result = await supabaseAuth(mode === 'sign-in' ? 'token?grant_type=password' : 'signup', {
        email: email.trim(),
        password,
      });

      if (!result.access_token) {
        setMessage('Account created. Check your email to confirm your account, then sign in.');
        setMode('sign-in');
        return;
      }

      saveSession(result);
      setSession(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Authentication failed.');
    } finally {
      setBusy(false);
    }
  }

  function signOut() {
    saveSession(null);
    setSession(null);
    setPassword('');
  }

  if (checking) {
    return <div className="flex min-h-screen items-center justify-center bg-background text-muted-foreground">Checking session…</div>;
  }

  if (session) {
    return (
      <>
        {children}
        <button
          type="button"
          onClick={signOut}
          className="fixed bottom-4 right-4 rounded-lg border bg-background px-3 py-2 text-xs font-medium shadow-sm hover:bg-muted"
        >
          Sign out
        </button>
      </>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="w-full max-w-md rounded-2xl border bg-card p-8 shadow-sm">
        <div className="mb-8">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Job Radar</p>
          <h1 className="mt-2 text-2xl font-semibold">{mode === 'sign-in' ? 'Welcome back' : 'Create your account'}</h1>
          <p className="mt-2 text-sm text-muted-foreground">Sign in to access your private job-search workspace.</p>
        </div>

        <form onSubmit={submit} className="space-y-4">
          <label className="block text-sm font-medium">
            Email
            <input
              type="email"
              required
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="mt-1.5 w-full rounded-lg border bg-background px-3 py-2.5 text-sm outline-none ring-offset-background focus:ring-2 focus:ring-ring"
            />
          </label>

          <label className="block text-sm font-medium">
            Password
            <input
              type="password"
              required
              minLength={6}
              autoComplete={mode === 'sign-in' ? 'current-password' : 'new-password'}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="mt-1.5 w-full rounded-lg border bg-background px-3 py-2.5 text-sm outline-none ring-offset-background focus:ring-2 focus:ring-ring"
            />
          </label>

          {error && <p className="rounded-lg bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>}
          {message && <p className="rounded-lg bg-muted px-3 py-2 text-sm text-muted-foreground">{message}</p>}

          <button
            type="submit"
            disabled={busy}
            className="w-full rounded-lg bg-primary px-4 py-2.5 text-sm font-semibold text-primary-foreground disabled:opacity-50"
          >
            {busy ? 'Please wait…' : mode === 'sign-in' ? 'Sign in' : 'Create account'}
          </button>
        </form>

        <button
          type="button"
          onClick={() => {
            setMode(mode === 'sign-in' ? 'sign-up' : 'sign-in');
            setError('');
            setMessage('');
          }}
          className="mt-5 w-full text-sm text-muted-foreground hover:text-foreground"
        >
          {mode === 'sign-in' ? 'Need an account? Create one' : 'Already have an account? Sign in'}
        </button>
      </div>
    </div>
  );
}

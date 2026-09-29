import { useEffect, useState } from 'react';
import { Settings2, ShieldCheck, UserCog, Users, X } from 'lucide-react';
import App from './App';

const SESSION_KEY = 'jobradar.supabase.session';
const API_BASE = ((import.meta.env.VITE_API_URL as string | undefined)?.trim() || 'https://job-radar-nfgv.onrender.com').replace(/\/+$/, '');

function token() {
  try {
    return (JSON.parse(sessionStorage.getItem(SESSION_KEY) || '{}') as { access_token?: string }).access_token || '';
  } catch {
    return '';
  }
}

async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Authorization', `Bearer ${token()}`);
  headers.set('Content-Type', 'application/json');
  const response = await fetch(`${API_BASE}/api${path}`, { ...init, headers });
  if (!response.ok) {
    const body = await response.text();
    throw new Error(body || `Request failed (${response.status})`);
  }
  return response.status === 204 ? undefined as T : await response.json();
}

type Me = {
  id: string;
  email: string;
  role: 'USER' | 'ADMIN' | 'SUPER_ADMIN';
};

type Settings = {
  showNotifications: boolean;
  showMatching: boolean;
};

type ManagedUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: 'USER' | 'ADMIN' | 'SUPER_ADMIN';
  createdAt: string;
  updatedAt: string;
};

const isAdmin = (role?: Me['role']) => role === 'ADMIN' || role === 'SUPER_ADMIN';

export default function AdminControls() {
  const [me, setMe] = useState<Me | null>(null);
  const [roleLoading, setRoleLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [adminOpen, setAdminOpen] = useState(false);
  const [s, setS] = useState<Settings>({ showNotifications: false, showMatching: false });
  const [users, setUsers] = useState<ManagedUser[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [busyUserId, setBusyUserId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');

  useEffect(() => {
    api<Me>('/auth/me')
      .then(setMe)
      .catch(() => setMe(null))
      .finally(() => setRoleLoading(false));
  }, []);

  useEffect(() => {
    if (isAdmin(me?.role)) {
      api<Settings>('/workspace/settings').then(setS).catch(() => {});
    }
  }, [me?.role]);

  async function loadUsers() {
    if (me?.role !== 'SUPER_ADMIN') return;
    setUsersLoading(true);
    setMessage('');
    try {
      setUsers(await api<ManagedUser[]>('/users'));
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not load users.');
    } finally {
      setUsersLoading(false);
    }
  }

  useEffect(() => {
    if (me?.role === 'SUPER_ADMIN' && open) void loadUsers();
  }, [me?.role, open]);

  if (roleLoading || !isAdmin(me?.role)) return null;

  const save = async (next: Settings) => {
    setBusy(true);
    setMessage('');
    try {
      const value = await api<Settings>('/workspace/settings', {
        method: 'PUT',
        body: JSON.stringify(next),
      });
      setS(value);
      setMessage('Saved for all users.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not save.');
    } finally {
      setBusy(false);
    }
  };

  const setAdminAccess = async (user: ManagedUser, enabled: boolean) => {
    setBusyUserId(user.id);
    setMessage('');
    try {
      const updated = await api<ManagedUser>(`/users/${user.id}/admin-access`, {
        method: 'PATCH',
        body: JSON.stringify({ enabled }),
      });
      setUsers((current) => current.map((item) => item.id === updated.id ? updated : item));
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not update admin access.');
    } finally {
      setBusyUserId(null);
    }
  };

  return (
    <>
      {adminOpen && (
        <div className="fixed inset-0 z-[60] overflow-auto bg-background">
          <button
            type="button"
            onClick={() => setAdminOpen(false)}
            aria-label="Exit admin console"
            className="fixed right-4 top-20 z-[70] inline-flex min-h-10 items-center gap-2 rounded-md border bg-card px-3 py-2 text-xs font-semibold shadow-md hover:bg-muted focus-visible:outline-2 focus-visible:outline-primary"
          >
            <X size={15} aria-hidden="true" />
            Exit admin console
          </button>
          <App />
        </div>
      )}

      <button
        type="button"
        onClick={() => setOpen(true)}
        className="fixed bottom-4 left-4 z-40 inline-flex min-h-11 items-center gap-2 rounded-lg border bg-background px-3 py-2 text-xs font-semibold shadow-lg hover:bg-muted"
      >
        <Settings2 size={15} aria-hidden="true" />
        Admin workspace
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/30 p-4 sm:items-center">
          <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl border bg-card p-5 shadow-xl sm:p-6">
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[.18em] text-muted-foreground">Administration</p>
                <h2 className="mt-1 text-lg font-bold">Admin workspace</h2>
                <p className="mt-1 text-sm text-muted-foreground">
                  {me?.role === 'SUPER_ADMIN'
                    ? 'You have full administration access, including user access management.'
                    : 'Manage the Job Radar workspace and admin tools.'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label="Close admin workspace"
                className="shrink-0 rounded-lg p-2 hover:bg-muted focus-visible:outline-2 focus-visible:outline-primary"
              >
                <X size={18} aria-hidden="true" />
              </button>
            </div>

            <div className="mt-5 grid gap-3 sm:grid-cols-2">
              <button
                type="button"
                onClick={() => { setOpen(false); setAdminOpen(true); }}
                className="rounded-xl border bg-background p-4 text-left transition-colors hover:bg-muted focus-visible:outline-2 focus-visible:outline-primary"
              >
                <span className="flex items-center gap-2 text-sm font-semibold">
                  <Settings2 size={16} aria-hidden="true" />
                  Open admin console
                </span>
                <span className="mt-1 block text-xs text-muted-foreground">
                  Manage companies, sources, scans, source health, and workspace controls.
                </span>
              </button>

              {me?.role === 'SUPER_ADMIN' && (
                <div className="rounded-xl border bg-background p-4">
                  <div className="flex items-center gap-2 text-sm font-semibold">
                    <Users size={16} aria-hidden="true" />
                    User management
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Grant or remove ADMIN access. Super admin accounts are protected.
                  </p>
                </div>
              )}
            </div>

            <div className="mt-5 space-y-2">
              {[
                ['showNotifications', 'Notifications', 'Show the alert history page'],
                ['showMatching', 'Matching', 'Show scoring details'],
              ].map(([key, label, detail]) => (
                <label key={key} className="flex cursor-pointer items-center justify-between gap-4 rounded-xl border p-4">
                  <span>
                    <span className="block text-sm font-semibold">{label}</span>
                    <span className="block text-xs text-muted-foreground">{detail}</span>
                  </span>
                  <input
                    type="checkbox"
                    checked={(s as Record<string, boolean>)[key]}
                    disabled={busy}
                    onChange={(event) => save({ ...s, [key]: event.target.checked })}
                    className="h-4 w-4"
                  />
                </label>
              ))}
            </div>

            {me?.role === 'SUPER_ADMIN' && (
              <section className="mt-6 border-t pt-6" aria-labelledby="user-management-heading">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[.18em] text-muted-foreground">Super admin</p>
                    <h3 id="user-management-heading" className="mt-1 text-base font-bold">Users</h3>
                    <p className="mt-1 text-sm text-muted-foreground">
                      Control who can access the admin workspace. This does not change their password or Supabase account.
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => void loadUsers()}
                    disabled={usersLoading}
                    className="inline-flex min-h-10 items-center gap-2 rounded-md border px-3 py-2 text-xs font-semibold hover:bg-muted disabled:opacity-50"
                  >
                    <Users size={15} aria-hidden="true" />
                    {usersLoading ? 'Refreshing…' : 'Refresh'}
                  </button>
                </div>

                <div className="mt-4 overflow-hidden rounded-xl border">
                  {usersLoading && users.length === 0 ? (
                    <div className="p-5 text-sm text-muted-foreground">Loading users…</div>
                  ) : users.length === 0 ? (
                    <div className="p-5 text-sm text-muted-foreground">No users have accessed Job Radar yet.</div>
                  ) : (
                    <div className="divide-y">
                      {users.map((user) => {
                        const protectedUser = user.role === 'SUPER_ADMIN';
                        const updating = busyUserId === user.id;
                        return (
                          <div key={user.id} className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between">
                            <div className="min-w-0">
                              <div className="flex items-center gap-2">
                                <UserCog size={16} className="shrink-0 text-muted-foreground" aria-hidden="true" />
                                <p className="truncate text-sm font-semibold">{user.displayName || user.email}</p>
                              </div>
                              <p className="mt-1 truncate pl-6 text-xs text-muted-foreground">{user.email}</p>
                              <p className="mt-1 pl-6 text-[11px] text-muted-foreground">
                                Joined {new Date(user.createdAt).toLocaleDateString()}
                              </p>
                            </div>

                            <div className="flex items-center justify-between gap-3 sm:justify-end">
                              <span className="inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-semibold">
                                {protectedUser && <ShieldCheck size={13} aria-hidden="true" />}
                                {user.role === 'SUPER_ADMIN' ? 'Super admin' : user.role === 'ADMIN' ? 'Admin' : 'User'}
                              </span>

                              {protectedUser ? (
                                <span className="text-xs text-muted-foreground">Protected</span>
                              ) : (
                                <button
                                  type="button"
                                  onClick={() => void setAdminAccess(user, user.role !== 'ADMIN')}
                                  disabled={updating}
                                  className="inline-flex min-h-10 items-center rounded-md border px-3 py-2 text-xs font-semibold hover:bg-muted disabled:opacity-50"
                                >
                                  {updating
                                    ? 'Saving…'
                                    : user.role === 'ADMIN'
                                      ? 'Remove admin'
                                      : 'Make admin'}
                                </button>
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              </section>
            )}

            {message && (
              <p role="status" className="mt-4 rounded-lg bg-secondary px-3 py-2 text-xs">
                {message}
              </p>
            )}

            <div className="mt-5 text-xs text-muted-foreground">
              {me?.role === 'SUPER_ADMIN'
                ? 'Super admin = all user access + admin access + user administration.'
                : 'Admin = all user access + admin workspace access.'}
            </div>
          </div>
        </div>
      )}
    </>
  );
}

// The platform's auth helpers, with the platform's exact contract: the same sessionStorage keys,
// the same exported functions, the same fail-open permission check. Application pages import
// hasPermission() and getUserData() from here; the shell uses login()/logout()/homeRouteForUser().
//
// Differences from the platform copy are confined to two lines, both marked "dev host".

import environment from './config/environment';
import { apps } from './apps';

const BASE_URL = environment.API_BASE_URL;

interface LoginResponse {
  success: boolean;
  message?: string;
  data?: any;
}

export function isApiUrl(url: string): boolean {
  return url.startsWith(BASE_URL);
}

export function isLoginUrl(url: string): boolean {
  return url === `${BASE_URL}/api/User/Login`;
}

export function getToken(): string | null {
  return sessionStorage.getItem('authToken');
}

export function getUserData(): any | null {
  const data = sessionStorage.getItem('userData');
  return data ? JSON.parse(data) : null;
}

export function isLoggedIn(): boolean {
  return getToken() !== null;
}

export async function login(email: string, password: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/api/User/Login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ Email: email, Password: password }),
  });

  if (!res.ok) {
    const text = await res.text().catch(() => '');
    let message = text;
    try { message = (JSON.parse(text) as { message?: string }).message ?? text; } catch { /* not JSON */ }
    throw new Error(message || `HTTP ${res.status}`);
  }

  const response = (await res.json()) as LoginResponse;
  // The platform's login answer carries the JWT in data.password; the dev host sends it in
  // data.token as well. Accept either.
  const authToken = response.data?.token ?? response.data?.password;

  if (!response.success || !authToken) {
    throw new Error(response.message || 'Unable to authenticate.');
  }

  sessionStorage.setItem('authToken', authToken);
  sessionStorage.setItem('userData', JSON.stringify(response.data));
  rememberAssignedYards(response.data?.yards);
}

const YARDS_KEY = 'pw_selectedYards';
const YARDS_CONFIRMED = 'pw_yardsConfirmed';

function rememberAssignedYards(yards: unknown): void {
  if (!Array.isArray(yards)) {
    sessionStorage.removeItem(YARDS_KEY);
    sessionStorage.removeItem(YARDS_CONFIRMED);
    return;
  }
  const next = yards.filter(y => {
    if (!y || typeof y !== 'object') return false;
    const row = y as { yard_type_id?: number; yard_type_name?: string };
    return Number(row.yard_type_id) > 0 && !!row.yard_type_name;
  });
  // One yard opens on its own. More than one waits for the selection screen.
  if (next.length === 1) {
    sessionStorage.setItem(YARDS_KEY, JSON.stringify(next));
    sessionStorage.setItem(YARDS_CONFIRMED, '1');
  } else {
    sessionStorage.removeItem(YARDS_KEY);
    sessionStorage.removeItem(YARDS_CONFIRMED);
  }
}

export function isAdmin(): boolean {
  const data = getUserData();
  return data?.is_admin === true || data?.is_super_admin === true;
}

// The first page this user's role can actually open. In the platform this is a fixed list
// (Data Management first, then each application); in the dev host the candidates are the
// applications in the manifest, in order.
export function homeRouteForUser(): string {
  const candidates: Array<[string, string]> = apps.map(a => [a.permissionCode, a.frontend.routeBase]);
  if (!candidates.length) return '/';
  const data = getUserData();
  if (!data || data.is_super_admin === true) return candidates[0][1];
  const perms: Array<{ permission_code: string }> = data.permissions ?? [];
  if (!perms.length) return candidates[0][1];
  const have = new Set(perms.map(p => p.permission_code));
  return candidates.find(([code]) => have.has(code))?.[1] ?? candidates[0][1];
}

export function hasPermission(code: string): boolean {
  try {
    const data = getUserData();
    if (!data) return true;
    if (data.is_super_admin === true) return true;
    const perms: Array<{ permission_code: string }> = data.permissions ?? [];
    if (!perms.length) return true; // fail-open: permissions not yet seeded
    return perms.some(p => p.permission_code === code || p.permission_code === '*');
  } catch {
    return true;
  }
}

export function getSectionPermPrefix(basePath: string): string {
  if (basePath.includes('oem-master')) return 'OM';
  if (basePath.includes('platform-master')) return 'PM';
  if (basePath.includes('global-master')) return 'GM';
  if (basePath.includes('project')) return 'PP';
  return 'GM';
}

export function logout(): void {
  sessionStorage.clear();
  window.location.href = '/login';
}

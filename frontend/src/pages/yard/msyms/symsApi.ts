import { BASE_URL } from '../../../api';
import { getToken, logout } from '../../../auth';
import { track } from '../../../services/apiActivity';

/**
 * The one seam between the untouched mSYMS screens and the platform.
 *
 * Every screen calls `fetch('http://localhost:4000/api/...')` with its own bearer header.
 * Rather than rewrite 53 URL literals — which would make the screens hard to diff against
 * upstream mSYMS — each call site swaps `fetch(` for `symsFetch(` and this does the work
 * the platform's own `trackedFetch` does for every other screen:
 *
 *   1. Rewrites the standalone mSYMS host to the platform's compatibility API.
 *   2. Attaches the platform bearer token, overriding whatever the screen set.
 *   3. Names the tenant (project / sub-project), which standalone mSYMS never had
 *      because it was single-tenant with one database.
 *   4. Counts the request in the global loader, so Slab Yard shows the same spinner as
 *      the rest of the platform instead of nothing.
 *   5. Signs the user out on a 401, so an expired session lands on the login screen
 *      rather than leaving every panel silently empty.
 *
 * The compatibility API answers mSYMS-shaped JSON at mSYMS-shaped paths, so the screens
 * cannot tell the difference.
 */
const MSYMS_ORIGIN = 'http://localhost:4000/api';
const COMPAT_BASE = `${BASE_URL}/api/yard/compat`;

let tenant: { projectId: string; subProjectId: string } = { projectId: '', subProjectId: '' };
let materialTypeKey = '';
let facilityTypeId = '';
let yardTypeId = '';

/**
 * Set by the mSYMS root from ProjectContext. Held in a module variable because the fetch
 * calls live inside plain functions in the screens, not inside components, so they cannot
 * read React context.
 */
export function setSymsTenant(next: { projectId: string; subProjectId: string }): void {
  tenant = next;
}

/** The material chosen after the facility. Empty until that step is done. */
export function setSymsMaterial(typeKey: string): void {
  materialTypeKey = typeKey;
}

/** Facility type chosen before the material, so a new area is tied to that facility. */
export function setSymsFacilityType(id: number | null): void {
  facilityTypeId = id && id > 0 ? String(id) : '';
}

/** The yard open on the left (Slab Yard, Coil Yard). Empty on the yard picker. */
export function setSymsYardType(id: number | null): void {
  yardTypeId = id && id > 0 ? String(id) : '';
}

export function getSymsTenant(): { projectId: string; subProjectId: string } {
  return tenant;
}

function rewrite(rawUrl: string): string {
  if (!rawUrl.startsWith(MSYMS_ORIGIN)) return rawUrl;

  // Base only matters on a site build, where BASE_URL is empty and the path is relative.
  const url = new URL(rawUrl.replace(MSYMS_ORIGIN, COMPAT_BASE), window.location.origin);

  // Appended rather than assigned so a screen that already built a query string
  // (e.g. /slabs?quality_status=LOCKED_QA) keeps it.
  if (tenant.projectId) url.searchParams.set('projectId', tenant.projectId);
  if (tenant.subProjectId) url.searchParams.set('subProjectId', tenant.subProjectId);
  if (materialTypeKey) url.searchParams.set('materialType', materialTypeKey);
  if (facilityTypeId) url.searchParams.set('facilityTypeId', facilityTypeId);
  // The open menu is the address (?yardTypeId=). Prefer that over the module variable so a
  // screen that fetches during the first paint still names the yard the operator clicked.
  const fromPage = new URLSearchParams(window.location.search).get('yardTypeId');
  const yard = yardTypeId || (fromPage && Number(fromPage) > 0 ? fromPage : '');
  if (yard) url.searchParams.set('yardTypeId', yard);

  return url.toString();
}

export async function symsFetch(input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> {
  const raw = typeof input === 'string' ? input : input.toString();

  const headers = new Headers(init.headers as HeadersInit | undefined);
  const token = getToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  // The tenant travels twice: as query parameters, which is what the compatibility
  // controller binds, and as the headers every other platform call sends. The headers
  // cost nothing and mean anything that inspects a request — a log, a proxy, a future
  // middleware — sees Slab Yard the same way it sees the rest of the app.
  if (tenant.projectId) headers.set('X-Project-Id', tenant.projectId);
  if (tenant.subProjectId) headers.set('X-Sub-Project-Id', tenant.subProjectId);

  // Several mSYMS calls send a JSON body without declaring the content type, relying on
  // Express to be lenient. ASP.NET is not.
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');

  const res = await track(fetch(rewrite(raw), { ...init, headers }));

  // Only sign out a session that actually exists — the same guard api.ts applies, for
  // the same reason: calling logout() with no token redirects to /login, which remounts
  // and fetches again.
  if (res.status === 401 && getToken()) logout();

  return res;
}

/**
 * The message a compat endpoint sends when it refuses.
 *
 * The screens vary between reading `err.error`, `err.message` and nothing at all, and
 * several show `undefined` to the operator as a result. This reads whichever the body
 * carries and falls back to something a person can act on.
 */
export async function symsError(res: Response, fallback = 'That did not work.'): Promise<string> {
  try {
    const body = await res.json();
    return body?.error || body?.message || fallback;
  } catch {
    if (res.status === 403) return 'Your role does not allow this.';
    return fallback;
  }
}

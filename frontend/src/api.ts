// The platform's API client, as an application sees it. Same exports, same behaviour:
//   - attaches the bearer token and the X-Project-Id / X-Sub-Project-Id headers from the current
//     selection on every call;
//   - counts every request in apiActivity, which drives the platform's global loader;
//   - throws on any non-2xx response (the body text is the error message), and signs the user out
//     on a 401.
// Applications import authGet / authPost / authPut / authDelete from here and never build
// headers or URLs by hand.

import { getToken, logout } from './auth';
import { track } from './services/apiActivity';
import type { ApiResponse } from './types';

export const BASE_URL: string =
  ((import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:9950').replace(/\/+$/, '');

export const PROJECT_KEY = 'pw_projectId';
export const SUB_PROJECT_KEY = 'pw_subProjectId';

type Params = Record<string, string | number | boolean | null | undefined>;

function headers(json: boolean): Record<string, string> {
  const h: Record<string, string> = {};
  if (json) h['Content-Type'] = 'application/json';
  const token = getToken();
  if (token) h.Authorization = `Bearer ${token}`;
  const projectId = sessionStorage.getItem(PROJECT_KEY);
  if (projectId) h['X-Project-Id'] = projectId;
  const subProjectId = sessionStorage.getItem(SUB_PROJECT_KEY);
  if (subProjectId) h['X-Sub-Project-Id'] = subProjectId;
  return h;
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    if (res.status === 401 && getToken()) logout();
    const text = await res.text();
    throw new Error(text || `HTTP ${res.status}`);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

function query(params?: Params): string {
  if (!params) return '';
  const entries = Object.entries(params)
    .filter(([, v]) => v != null)
    .map(([k, v]) => [k, String(v)] as [string, string]);
  return entries.length ? '?' + new URLSearchParams(entries).toString() : '';
}

export function authGet<T>(url: string, params?: Params): Promise<T> {
  return track(fetch(url + query(params), { headers: headers(false) }).then(r => handleResponse<T>(r)));
}

export function authPost<T>(url: string, body: unknown): Promise<T> {
  return track(fetch(url, { method: 'POST', headers: headers(true), body: JSON.stringify(body) }).then(r => handleResponse<T>(r)));
}

export function authPut<T>(url: string, body: unknown): Promise<T> {
  return track(fetch(url, { method: 'PUT', headers: headers(true), body: JSON.stringify(body) }).then(r => handleResponse<T>(r)));
}

export function authDelete<T>(url: string, body?: unknown): Promise<T> {
  return track(fetch(url, {
    method: 'DELETE',
    headers: headers(body !== undefined),
    body: body === undefined ? undefined : JSON.stringify(body),
  }).then(r => handleResponse<T>(r)));
}

// Older aliases the platform also exports.
export const getJson = authGet;
export const postJson = authPost;
export const putJson = authPut;
export function deleteReq<T>(url: string): Promise<T> {
  return authDelete<T>(url);
}

export type { ApiResponse };

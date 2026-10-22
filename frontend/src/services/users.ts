import { BASE_URL, authDelete, authGet, authPost, authPut } from '../api';
import type { ApiResponse } from '../types';

const BASE = `${BASE_URL}/api/users`;

export interface ManagedYard {
  yard_type_id: number;
  yard_type_name: string;
  description?: string | null;
}

export interface ManagedUser {
  id: number;
  first_name: string;
  last_name: string;
  email: string;
  phone: string | null;
  is_admin: boolean;
  is_active: boolean;
  yards: ManagedYard[];
}

export interface UserInput {
  first_name: string;
  last_name: string;
  email: string;
  phone: string;
  password: string;
  is_admin: boolean;
  is_active: boolean;
  yard_type_ids: number[];
}

async function attempt<T>(call: () => Promise<ApiResponse<T>>): Promise<ApiResponse<T>> {
  try { return await call(); }
  catch (err) { return { success: false, message: messageFrom(err), data: null }; }
}

function messageFrom(err: unknown): string {
  const text = err instanceof Error ? err.message : String(err);
  try {
    const parsed = JSON.parse(text) as Partial<ApiResponse<unknown>>;
    if (typeof parsed.message === 'string') return parsed.message;
  } catch { /* not JSON */ }
  return text || 'Request failed.';
}

export const listUsers = () => attempt(() => authGet<ApiResponse<ManagedUser[]>>(BASE));

export const createUser = (input: UserInput) =>
  attempt(() => authPost<ApiResponse<ManagedUser>>(BASE, input));

export const updateUser = (id: number, input: UserInput) =>
  attempt(() => authPut<ApiResponse<ManagedUser>>(`${BASE}/${id}`, input));

export const setUserActive = (id: number, is_active: boolean) =>
  attempt(() => authPut<ApiResponse<ManagedUser>>(`${BASE}/${id}/active`, { is_active }));

export const deleteUser = (id: number) =>
  attempt(() => authDelete<ApiResponse<ManagedUser>>(`${BASE}/${id}`));

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { BASE_URL, authGet } from '../api';
import { getUserData } from '../auth';
import type { ApiResponse } from '../types';

const KEY = 'pw_selectedYards';
const CONFIRMED = 'pw_yardsConfirmed';

export interface SelectedYard {
  yard_type_id: number;
  yard_type_name: string;
  description?: string | null;
}

function read(): SelectedYard[] {
  try {
    const raw = sessionStorage.getItem(KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as SelectedYard[];
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(y => y && Number(y.yard_type_id) > 0 && !!y.yard_type_name);
  } catch {
    return [];
  }
}

interface SessionProfile {
  first_name?: string;
  last_name?: string;
  email?: string;
  role_name?: string;
  is_admin?: boolean;
  is_super_admin?: boolean;
  permissions?: unknown;
  yards?: SelectedYard[];
}

interface YardSelectionContextValue {
  /** Yards this account is allowed to open. */
  assigned: SelectedYard[];
  /** Yards currently shown in the left menu. */
  selected: SelectedYard[];
  /** False until the signed-in account's yards have been loaded. */
  ready: boolean;
  setSelected: (yards: SelectedYard[]) => void;
  /** Replace the account's yard list, for example after an administrator edits their own user. */
  replaceAssigned: (yards: SelectedYard[]) => void;
}

const YardSelectionContext = createContext<YardSelectionContextValue | null>(null);

function normalize(yards: SelectedYard[] | undefined): SelectedYard[] {
  return (yards ?? []).filter(y => Number(y.yard_type_id) > 0 && !!y.yard_type_name);
}

function write(yards: SelectedYard[], confirmed: boolean): void {
  if (yards.length) sessionStorage.setItem(KEY, JSON.stringify(yards));
  else sessionStorage.removeItem(KEY);
  if (confirmed) sessionStorage.setItem(CONFIRMED, '1');
  else sessionStorage.removeItem(CONFIRMED);
}

/** A single assigned yard is opened for them. Several stay closed until they pick. */
function openFrom(allowed: SelectedYard[], preferSaved: boolean): SelectedYard[] {
  if (allowed.length <= 1) return allowed;
  if (!preferSaved || sessionStorage.getItem(CONFIRMED) !== '1') return [];
  const ids = new Set(allowed.map(y => y.yard_type_id));
  return read().filter(y => ids.has(y.yard_type_id));
}

export function YardSelectionProvider({ children }: { children: ReactNode }) {
  const [assigned, setAssigned] = useState<SelectedYard[]>([]);
  const [selected, setSelectedState] = useState<SelectedYard[]>(read);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    authGet<ApiResponse<SessionProfile>>(`${BASE_URL}/api/User/me`)
      .then(res => {
        if (cancelled || !res.success || !res.data) return;
        const stored = getUserData();
        if (stored) {
          stored.first_name = res.data.first_name;
          stored.last_name = res.data.last_name;
          stored.email = res.data.email;
          stored.role_name = res.data.role_name;
          stored.is_admin = res.data.is_admin;
          stored.is_super_admin = res.data.is_admin === true;
          stored.permissions = res.data.permissions;
          stored.yards = res.data.yards;
          sessionStorage.setItem('userData', JSON.stringify(stored));
        }
        const allowed = normalize(res.data.yards);
        const chosen = openFrom(allowed, true);
        setAssigned(allowed);
        setSelectedState(chosen);
        write(chosen, allowed.length <= 1 || (chosen.length > 0 && sessionStorage.getItem(CONFIRMED) === '1'));
      })
      .catch(() => { /* keep the yards stored at sign-in */ })
      .finally(() => { if (!cancelled) setReady(true); });
    return () => { cancelled = true; };
  }, []);

  const value = useMemo<YardSelectionContextValue>(() => ({
    assigned,
    selected,
    ready,
    setSelected: (yards) => {
      const ids = new Set(assigned.map(y => y.yard_type_id));
      const next = yards.filter(y => y.yard_type_id > 0 && y.yard_type_name && (ids.size === 0 || ids.has(y.yard_type_id)));
      setSelectedState(next);
      write(next, true);
    },
    replaceAssigned: (yards) => {
      const allowed = normalize(yards);
      const chosen = openFrom(allowed, true);
      setAssigned(allowed);
      setSelectedState(chosen);
      write(chosen, allowed.length <= 1 || chosen.length > 0);
    },
  }), [assigned, selected, ready]);

  return <YardSelectionContext.Provider value={value}>{children}</YardSelectionContext.Provider>;
}

export function useYardSelection(): YardSelectionContextValue {
  const ctx = useContext(YardSelectionContext);
  if (!ctx) throw new Error('useYardSelection must be used within YardSelectionProvider');
  return ctx;
}

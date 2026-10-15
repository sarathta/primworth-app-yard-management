import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';

const KEY = 'pw_selectedYards';

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

interface YardSelectionContextValue {
  selected: SelectedYard[];
  setSelected: (yards: SelectedYard[]) => void;
}

const YardSelectionContext = createContext<YardSelectionContextValue | null>(null);

export function YardSelectionProvider({ children }: { children: ReactNode }) {
  const [selected, setSelectedState] = useState<SelectedYard[]>(read);

  const value = useMemo<YardSelectionContextValue>(() => ({
    selected,
    setSelected: (yards) => {
      const next = yards.filter(y => y.yard_type_id > 0 && y.yard_type_name);
      setSelectedState(next);
      if (next.length) sessionStorage.setItem(KEY, JSON.stringify(next));
      else sessionStorage.removeItem(KEY);
    },
  }), [selected]);

  return <YardSelectionContext.Provider value={value}>{children}</YardSelectionContext.Provider>;
}

export function useYardSelection(): YardSelectionContextValue {
  const ctx = useContext(YardSelectionContext);
  if (!ctx) throw new Error('useYardSelection must be used within YardSelectionProvider');
  return ctx;
}

import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react';
import Loader from './Loader';

// ─────────────────────────────────────────────────────────────────────────────
// LoaderProvider — global loader state + the overlay it controls.
//
// Exposes showLoader() / hideLoader(). A reference-counter coalesces overlapping
// calls: every showLoader() increments, every hideLoader() decrements, and the
// overlay is visible while the count is > 0. This makes parallel API calls safe —
// the loader appears on the first request and only hides once the last finishes
// (no flicker, no early hide). Always pair the calls with try/finally so the count
// can't leak on errors.
//
// Reusable by any screen via useLoader(); the Recipe screen additionally bridges
// the HTTP layer's in-flight count into these methods (see RecipePage).
// ─────────────────────────────────────────────────────────────────────────────

interface LoaderApi {
  showLoader: () => void;
  hideLoader: () => void;
  loading: boolean;
}

const LoaderContext = createContext<LoaderApi | null>(null);

export function LoaderProvider({ children }: { children: React.ReactNode }): React.JSX.Element {
  const countRef = useRef(0);
  const [loading, setLoading] = useState(false);

  const showLoader = useCallback(() => {
    countRef.current += 1;
    if (countRef.current > 0) setLoading(true);
  }, []);

  const hideLoader = useCallback(() => {
    countRef.current = Math.max(0, countRef.current - 1);
    if (countRef.current === 0) setLoading(false);
  }, []);

  const value = useMemo<LoaderApi>(() => ({ showLoader, hideLoader, loading }), [showLoader, hideLoader, loading]);

  return (
    <LoaderContext.Provider value={value}>
      {children}
      <Loader visible={loading} />
    </LoaderContext.Provider>
  );
}

// Access the global loader controls. Throws if used outside <LoaderProvider>.
export function useLoader(): LoaderApi {
  const ctx = useContext(LoaderContext);
  if (!ctx) throw new Error('useLoader must be used within a LoaderProvider');
  return ctx;
}

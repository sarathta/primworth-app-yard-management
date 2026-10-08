import React, { createContext, useContext, useEffect, useRef, useState } from 'react';
import { symsFetch } from '../symsApi';

/**
 * mSYMS's socket context, re-implemented as a poller with a real health check.
 *
 * The original connected to a Socket.io server at localhost:4000. The platform API is .NET
 * with no socket transport to the browser, so rather than edit every screen this exposes
 * the same `{ socket }` surface and drives the registered handlers on a timer.
 *
 * That substitution is behaviourally sound here because every mSYMS handler does the same
 * thing — refetch. Dashboard.tsx binds six different events and points all of them at
 * `fetchDashboard`. Firing subscribers periodically reproduces the observable behaviour;
 * the only loss is latency between a change and the screen noticing it.
 *
 * `connected` used to be `!!socket`, which was set unconditionally on mount and therefore
 * true from first paint for ever. That drove two indicators — the "Yard link online" strip
 * above every screen and the operator tablet's SYNCED badge — so a tablet with a dead API
 * showed a green light over an empty worklist. It now means what it says: the API answered
 * the last time we asked it. A refused answer still counts as reachable, because "your
 * role does not allow this" is the server talking, not a broken link.
 *
 * Interface kept minimal on purpose: the screens only ever call `on` and `off`.
 */
export interface SymsSocket {
  on(event: string, handler: SocketHandler): void;
  off(event: string, handler?: SocketHandler): void;
  emit(event: string, ...args: unknown[]): void;
}

type SocketHandler = (...args: unknown[]) => void;

interface SocketContextData {
  socket: SymsSocket | null;
  connected: boolean;
  /** When the API last answered. Null until the first probe settles. */
  lastContactAt: Date | null;
}

const SocketContext = createContext<SocketContextData>({
  socket: null,
  connected: false,
  lastContactAt: null,
});

/** How often registered handlers are fired. Matches the platform's other background polls. */
const POLL_MS = 15000;

/**
 * The probe. Cheap, read-only, and available to every yard role, so it measures the link
 * rather than the caller's permissions.
 */
const HEALTH_URL = 'http://localhost:4000/api/config';

export const SocketProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const handlers = useRef(new Map<string, Set<SocketHandler>>());
  const [socket, setSocket] = useState<SymsSocket | null>(null);
  const [connected, setConnected] = useState(false);
  const [lastContactAt, setLastContactAt] = useState<Date | null>(null);

  useEffect(() => {
    let alive = true;

    const api: SymsSocket = {
      on(event, handler) {
        const set = handlers.current.get(event) ?? new Set<SocketHandler>();
        set.add(handler);
        handlers.current.set(event, set);
      },
      off(event, handler) {
        // mSYMS calls off() both with and without the handler; support both so cleanup
        // functions written either way actually unsubscribe.
        if (!handler) { handlers.current.delete(event); return; }
        handlers.current.get(event)?.delete(handler);
      },
      emit() {
        // The screens never emit; nothing upstream is listening.
      },
    };

    setSocket(api);

    async function probe(): Promise<void> {
      try {
        const res = await symsFetch(HEALTH_URL);
        // 403 is the server refusing, not the link being down. Either way it answered.
        const reachable = res.ok || res.status === 403;
        if (!alive) return;
        setConnected(reachable);
        if (reachable) setLastContactAt(new Date());
      } catch {
        if (alive) setConnected(false);
      }
    }

    function fire(): void {
      // Deduplicated: Dashboard binds the same refetch to six events, and firing it six
      // times per tick would multiply every poll into six requests.
      const fired = new Set<SocketHandler>();
      handlers.current.forEach(set => set.forEach(h => {
        if (fired.has(h)) return;
        fired.add(h);
        try { h(); } catch { /* a failing screen handler must not stop the others */ }
      }));
    }

    // Probe immediately so the strip is right on first paint rather than after 15 s.
    void probe();

    const timer = window.setInterval(() => { void probe(); fire(); }, POLL_MS);

    return () => { alive = false; window.clearInterval(timer); };
  }, []);

  return (
    <SocketContext.Provider value={{ socket, connected, lastContactAt }}>
      {children}
    </SocketContext.Provider>
  );
};

export const useSocket = () => useContext(SocketContext);

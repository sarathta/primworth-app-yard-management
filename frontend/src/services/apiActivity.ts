// ─────────────────────────────────────────────────────────────────────────────
// apiActivity — a tiny, framework-agnostic tracker of in-flight HTTP requests.
//
// The HTTP layer (api.ts) calls begin() when a request starts and end() when it
// settles (success OR failure). Subscribers are notified with the current count of
// in-flight requests, so a global loader can show while count > 0 and hide at 0 —
// naturally coalescing parallel requests (no flicker, no early hide).
//
// This module holds NO React and changes NO request/response behavior; it only
// emits start/end signals. Consumers opt in by subscribing (see LoaderProvider /
// the Recipe screen), so screens that don't subscribe are completely unaffected.
// ─────────────────────────────────────────────────────────────────────────────

type Listener = (activeCount: number) => void;

let activeCount = 0;
const listeners = new Set<Listener>();

function emit(): void {
  for (const l of listeners) l(activeCount);
}

// Mark a request as started. Always pair with end() (use try/finally at the call site).
export function begin(): void {
  activeCount += 1;
  emit();
}

// Mark a request as finished (success or error). Clamped at 0 for safety.
export function end(): void {
  activeCount = Math.max(0, activeCount - 1);
  emit();
}

// Subscribe to in-flight count changes. Fires immediately with the current count and
// returns an unsubscribe function.
export function subscribe(listener: Listener): () => void {
  listeners.add(listener);
  listener(activeCount);
  return () => { listeners.delete(listener); };
}

// Wrap a promise so begin()/end() bracket its lifecycle (resolves or rejects).
export function track<T>(p: Promise<T>): Promise<T> {
  begin();
  return p.finally(end);
}

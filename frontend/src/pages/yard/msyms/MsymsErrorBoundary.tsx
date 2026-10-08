import React from 'react';

/**
 * Contains render errors from the embedded mSYMS screens.
 *
 * Without this, a single throw inside any mSYMS component unmounts the whole React tree —
 * the platform shell included — and the user gets a blank white page with no indication of
 * what happened or how to get back. That is the price of hosting a third-party UI in the
 * same document rather than an iframe, and it is worth paying only because the failure is
 * containable, which is what this does.
 *
 * The message deliberately names the module and shows the error, because these failures are
 * overwhelmingly data-shape mismatches between the compatibility API and what the screens
 * expect — and the message is the fastest route to the fix.
 */
interface Props { children: React.ReactNode }
interface State { error: Error | null }

export default class MsymsErrorBoundary extends React.Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    // Keep the component stack in the console; it identifies the screen that failed.
    console.error('[Slab Yard] render error:', error, info.componentStack);
  }

  render() {
    if (!this.state.error) return this.props.children;

    return (
      <div style={{ padding: '2rem', color: '#C5C6C7', background: '#0B0C10', minHeight: '100%' }}>
        <h2 style={{ color: '#fff', marginBottom: '0.5rem' }}>Slab Yard hit an error</h2>
        <p style={{ color: '#93969A', marginBottom: '1rem', maxWidth: '46rem' }}>
          This screen failed to render. The rest of the platform is unaffected — use the
          sidebar to navigate away, or reload to try again.
        </p>
        <pre style={{
          background: '#1F2833', border: '1px solid #333A44', borderRadius: 8,
          padding: '1rem', overflowX: 'auto', color: '#FF8FA3', fontSize: 13,
        }}>
          {this.state.error.message}
        </pre>
        <button
          type="button"
          onClick={() => this.setState({ error: null })}
          style={{
            marginTop: '1rem', padding: '0.5rem 1rem', borderRadius: 6,
            background: '#098446', color: '#fff', border: 0, cursor: 'pointer',
          }}
        >
          Try again
        </button>
      </div>
    );
  }
}

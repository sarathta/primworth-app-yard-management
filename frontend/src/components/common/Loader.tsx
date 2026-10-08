// Presentational global loader — a fullscreen overlay with a centered spinner.
// Stateless and reusable; the LoaderProvider decides when to render it.

interface LoaderProps {
  visible: boolean;
  label?: string;
}

export default function Loader({ visible, label = 'Loading…' }: LoaderProps): React.JSX.Element | null {
  if (!visible) return null;
  return (
    <div className="app-loader" role="alert" aria-busy="true" aria-live="assertive">
      <div className="app-loader__box">
        <div className="app-loader__spinner" />
        <span className="app-loader__label">{label}</span>
      </div>
    </div>
  );
}

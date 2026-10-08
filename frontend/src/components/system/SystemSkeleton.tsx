const PLACEHOLDERS = [1, 2, 3, 4, 5];

export default function SystemSkeleton(): React.JSX.Element {
  return (
    <div className="system-skeleton" aria-label="Loading">
      {PLACEHOLDERS.map(row => (
        <div key={row} className="system-skeleton__row">
          <span className="system-skeleton__avatar" />
          <span className="system-skeleton__line system-skeleton__line--wide" />
          <span className="system-skeleton__line" />
          <span className="system-skeleton__line system-skeleton__line--short" />
        </div>
      ))}
    </div>
  );
}

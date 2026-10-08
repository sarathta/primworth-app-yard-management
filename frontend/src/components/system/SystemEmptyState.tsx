export interface SystemEmptyStateProps {
  icon?: string;
  title?: string;
  message?: string;
}

export default function SystemEmptyState({
  icon = 'inbox',
  title = 'No data found',
  message = 'Try changing filters or refresh the page.',
}: SystemEmptyStateProps): React.JSX.Element {
  return (
    <div className="system-empty-state">
      <span className="material-symbols-outlined">{icon}</span>
      <h3>{title}</h3>
      <p>{message}</p>
    </div>
  );
}

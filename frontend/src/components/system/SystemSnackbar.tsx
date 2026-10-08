import type { useSystemSnackbar } from './useSystemSnackbar';

type SnackbarState = ReturnType<typeof useSystemSnackbar>['items'];

export interface SystemSnackbarProps {
  items: SnackbarState;
  onDismiss: (id: number) => void;
}

export default function SystemSnackbar({ items, onDismiss }: SystemSnackbarProps): React.JSX.Element {
  if (!items.length) return <></>;

  return (
    <div className="system-snackbar-container" aria-live="polite">
      {items.map(item => (
        <div key={item.id} className={`system-snackbar system-snackbar--${item.kind}`}>
          <span>{item.message}</span>
          <button type="button" className="system-snackbar__close" onClick={() => onDismiss(item.id)}>
            Close
          </button>
        </div>
      ))}
    </div>
  );
}

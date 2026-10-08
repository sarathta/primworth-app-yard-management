export interface SystemConfirmDialogData {
  title: string;
  message: React.ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

export interface SystemConfirmDialogProps extends SystemConfirmDialogData {
  onConfirm: () => void;
  onCancel: () => void;
}

export default function SystemConfirmDialog({
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  danger = false,
  onConfirm,
  onCancel,
}: SystemConfirmDialogProps): React.JSX.Element {
  function handleOverlayClick(e: React.MouseEvent<HTMLDivElement>): void {
    if (e.target === e.currentTarget) onCancel();
  }

  return (
    <div className="modal-overlay" onClick={handleOverlayClick}>
      <div className="modal-box modal-box--confirm">
        <div className="modal-header">
          <h3 className="system-dialog__title">
            <span
              className={`material-symbols-outlined${danger ? ' system-confirm__danger' : ''}`}
            >
              {danger ? 'warning' : 'help'}
            </span>
            {title}
          </h3>
        </div>
        <div className="system-dialog__content">
          <p>{message}</p>
        </div>
        <div className="system-dialog__actions">
          <button type="button" className="btn btn--secondary" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button
            type="button"
            className={`btn ${danger ? 'btn--danger' : 'btn--primary'}`}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

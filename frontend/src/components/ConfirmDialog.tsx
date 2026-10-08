// ConfirmDialog — reusable confirmation modal.
//
// Props:
//   title        (string)   — modal heading
//   message      (string)   — body text
//   confirmLabel (string)   — text on the confirm button (default "Confirm")
//   cancelLabel  (string)   — text on the cancel button  (default "Cancel")
//   danger       (boolean)  — true → confirm button is red (destructive action)
//   onConfirm    (function) — called when user clicks confirm
//   onCancel     (function) — called when user clicks cancel or the backdrop
//
// Usage:
//   {pendingItem && (
//     <ConfirmDialog
//       title="Delete Record"
//       message="This cannot be undone."
//       confirmLabel="Delete"
//       danger
//       onConfirm={handleConfirm}
//       onCancel={() => setPendingItem(null)}
//     />
//   )}

import type { ConfirmDialogProps } from '../types';

export default function ConfirmDialog({
  title        = 'Confirm',
  message      = 'Are you sure?',
  confirmLabel = 'Confirm',
  cancelLabel  = 'Cancel',
  danger       = false,
  onConfirm,
  onCancel,
  children,
  width,
}: ConfirmDialogProps): React.JSX.Element {
  // Close the modal when the user clicks the dark backdrop (not the box itself)
  function handleOverlayClick(e: React.MouseEvent<HTMLDivElement>): void {
    if (e.target === e.currentTarget) onCancel?.();
  }

  return (
    <div className="modal-overlay" onClick={handleOverlayClick}>
      <div
        className="modal-box"
        style={width != null ? { width, maxWidth: '95vw' } : undefined}
      >

        {/* Header */}
        <div className="modal-header">
          <span className={`material-symbols-outlined modal-icon${danger ? ' modal-icon--danger' : ''}`}>
            {danger ? 'warning' : 'help_outline'}
          </span>
          <h3>{title}</h3>
        </div>

        {/* Body */}
        <div className="modal-body">
          <p style={{ whiteSpace: 'pre-wrap' }}>{message}</p>
          {children && <div style={{ marginTop: '16px' }}>{children}</div>}
        </div>

        {/* Footer */}
        <div className="modal-footer">
          <button className="btn btn--secondary" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button
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

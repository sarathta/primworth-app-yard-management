// A form or detail dialog in the platform's frame (modal-overlay > modal-box > header / content /
// actions), and its footer: Cancel on the left, the action on the right — as every platform dialog.
import type { ReactNode } from 'react';
import { Icon } from './PageHeader';
import './page.css';

export function Modal({ title, icon, size = 'md', footer, children }: {
  title: ReactNode;
  /** A Material Symbols name shown before the title. */
  icon: string;
  /** confirm 460 px · md 620 px · lg 860 px · xl 1180 px. */
  size?: 'confirm' | 'md' | 'lg' | 'xl';
  footer?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="modal-overlay">
      <div className={`modal-box ${size === 'confirm' ? 'modal-box--confirm' : `modal-box--pg-${size}`}`} role="dialog" aria-modal="true">
        <div className="modal-header">
          <h3 className="system-dialog__title"><Icon name={icon} />{title}</h3>
        </div>
        <div className="system-dialog__content">{children}</div>
        {footer && <div className="system-dialog__actions">{footer}</div>}
      </div>
    </div>
  );
}

/** Cancel (secondary) then the action (primary); while busy the action reads "Saving..." and is disabled. */
export function DialogButtons({ onCancel, onSave, saveLabel = 'Save', busy, busyLabel = 'Saving...', disabled, cancelLabel = 'Cancel' }: {
  onCancel: () => void;
  onSave?: () => void;
  saveLabel?: string;
  busy?: boolean;
  busyLabel?: string;
  disabled?: boolean;
  cancelLabel?: string;
}) {
  return (
    <>
      <button type="button" className="btn btn--secondary" onClick={onCancel}>{cancelLabel}</button>
      {onSave && (
        <button type="button" className="btn btn--primary" disabled={busy || disabled} onClick={onSave}>
          {busy ? busyLabel : saveLabel}
        </button>
      )}
    </>
  );
}

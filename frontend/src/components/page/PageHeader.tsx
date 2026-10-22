// The top of an application page, as the platform's own pages have it (User Management, Projects):
// breadcrumb, title with a one-line description, and the page's actions on the right — the labelled
// primary action ("Add …") last. `aside` puts context on the right of the breadcrumb row (scope, a status
// light); `onReload` adds a Reload button for pages whose data changes under the user.
import type { ReactNode } from 'react';
import './page.css';

export const Icon = ({ name, className = '' }: { name: string; className?: string }) => (
  <span className={`material-symbols-outlined${className ? ` ${className}` : ''}`}>{name}</span>
);

export interface PageHeaderProps {
  /** Breadcrumb before the title, e.g. ['Roll Management']. */
  title: string;
  actions?: ReactNode;
  onReload?: () => void;
}

export function PageHeader({title,  actions, onReload }: PageHeaderProps) {
  return (
    <>
      <div className="page-header">
        <div>
          <h2>{title}</h2>
        </div>
        <div className="page-header__actions">
          {onReload && (
            <button type="button" className="btn btn--secondary" onClick={onReload} title="Reload this page's data">
              <Icon name="refresh" />Reload
            </button>
          )}
          {actions}
        </div>
      </div>
    </>
  );
}

/** The labelled primary action at the top right ("Add Roll"). A disabled one says why in its tooltip. */
export function AddButton({ label, onClick, disabled, disabledReason, icon = 'add' }: {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  disabledReason?: string;
  icon?: string;
}) {
  return (
    <span className="tooltip-wrap" title={disabled ? disabledReason : undefined}>
      <button type="button" className="btn btn--primary" disabled={disabled} onClick={onClick}>
        <Icon name={icon} />{label}
      </button>
    </span>
  );
}

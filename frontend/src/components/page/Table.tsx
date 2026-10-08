// Tables the platform way: a card with a progress bar while loading, a skeleton on the first load, the
// platform's empty state, the table, then the pager; headers that sort on click; and the actions at the
// end of each row as icon buttons whose tooltip says what they do — or, when greyed, why not.
import type { ReactNode } from 'react';
import SystemEmptyState from '../system/SystemEmptyState';
import SystemSkeleton from '../system/SystemSkeleton';
import { Icon } from './PageHeader';
import './page.css';

export function TableCard({ loading, hasRows, emptyIcon, emptyTitle, emptyMessage, header, footer, children }: {
  loading: boolean;
  hasRows: boolean;
  emptyIcon: string;
  emptyTitle: string;
  emptyMessage: string;
  /** A CardHeader when the card is one of several on the page. */
  header?: ReactNode;
  /** Usually <Pagination/>; shown only when there are rows. */
  footer?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="system-card table-card">
      {header}
      {loading && <div className="system-progress-bar"><div className="system-progress-bar__indeterminate" /></div>}
      {loading && !hasRows ? <SystemSkeleton />
        : !hasRows ? <SystemEmptyState icon={emptyIcon} title={emptyTitle} message={emptyMessage} />
          : <div className="table-scroll">{children}</div>}
      {hasRows && footer}
    </div>
  );
}

/** A card's own title row with a note and its actions (search box, Add button). */
export function CardHeader({ title, note, children }: { title: string; note?: string; children?: ReactNode }) {
  return (
    <div className="pg-card-header">
      <div>
        <h3>{title}</h3>
        {note && <p>{note}</p>}
      </div>
      {children && <div className="row-actions">{children}</div>}
    </div>
  );
}

/** An icon button (row actions, arrows, viewer tools). Greyed, its tooltip gives the reason instead. */
export function IconAction({ icon, title, onClick, disabled, disabledReason, variant }: {
  icon: string;
  title: string;
  onClick: () => void;
  disabled?: boolean;
  disabledReason?: string;
  variant?: 'danger' | 'warn' | 'primary' | 'info';
}) {
  return (
    <span className="tooltip-wrap" title={disabled && disabledReason ? disabledReason : title}>
      <button type="button" className={`btn-icon${variant ? ` btn-icon--${variant}` : ''}`} disabled={disabled} onClick={onClick} aria-label={title}>
        <Icon name={icon} />
      </button>
    </span>
  );
}

export interface Sort<K extends string> { field: K | null; dir: 'asc' | 'desc' | null }

/** Ascending, then descending, then unsorted — as the platform's User Management. */
export function nextSort<K extends string>(sort: Sort<K>, field: K): Sort<K> {
  if (sort.field !== field || !sort.dir) return { field, dir: 'asc' };
  return sort.dir === 'asc' ? { field, dir: 'desc' } : { field: null, dir: null };
}

export function SortTh<K extends string>({ label, field, sort, onSort, className }: {
  label: string; field: K; sort: Sort<K>; onSort: (s: Sort<K>) => void; className?: string;
}) {
  const icon = sort.field !== field || !sort.dir ? 'unfold_more' : sort.dir === 'asc' ? 'arrow_upward' : 'arrow_downward';
  return (
    <th className={`sortable${className ? ` ${className}` : ''}`} onClick={() => onSort(nextSort(sort, field))}>
      {label}<Icon name={icon} className="sort-icon" />
    </th>
  );
}

/** Sorts a copy of rows client-side by a key (text case-insensitively). */
export function sortRows<T, K extends string>(rows: T[], sort: Sort<K>, value: (row: T, key: K) => string | number | null | undefined): T[] {
  if (!sort.field || !sort.dir) return rows;
  const key = sort.field;
  const dir = sort.dir === 'asc' ? 1 : -1;
  const norm = (v: string | number | null | undefined) => (typeof v === 'string' ? v.toLowerCase() : v ?? '');
  return [...rows].sort((a, b) => {
    const x = norm(value(a, key));
    const y = norm(value(b, key));
    return x > y ? dir : x < y ? -dir : 0;
  });
}

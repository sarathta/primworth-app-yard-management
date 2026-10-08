// Fields as platform forms build them (SystemField around a native control), and the filter card that
// sits between the page header and the table: as many filters as the page needs, then Reset Filters.
import type { InputHTMLAttributes, ReactNode } from 'react';
import SystemField from '../system/SystemField';
import { Icon } from './PageHeader';
import './page.css';

export interface SelectOption { value: string; label: string }

export function SelectField({ label, value, options, onChange, placeholder, disabled, error, className }: {
  label: string;
  value: string;
  options: SelectOption[];
  onChange: (value: string) => void;
  /** A first, empty option ("All mills", "Select type"). */
  placeholder?: string;
  disabled?: boolean;
  error?: string;
  className?: string;
}) {
  return (
    <div className={className}>
      <SystemField label={label} error={error}>
        <select className="system-field__control system-field__control--select" value={value} disabled={disabled} onChange={e => onChange(e.target.value)}>
          {placeholder !== undefined && <option value="">{placeholder}</option>}
          {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      </SystemField>
    </div>
  );
}

export function TextField({ label, value, onChange, error, className, prefixIcon, hint, ...input }: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  className?: string;
  prefixIcon?: string;
  /** Shown under the field (use <span className="pg-hint">). */
  hint?: ReactNode;
} & Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'className'>) {
  return (
    <div className={className}>
      <SystemField label={label} error={error} prefixIcon={prefixIcon}>
        <input className="system-field__control" value={value} onChange={e => onChange(e.target.value)} autoComplete="off" {...input} />
      </SystemField>
      {hint}
    </div>
  );
}

export function TextAreaField({ label, value, onChange, maxLength, className, rows = 3 }: {
  label: string; value: string; onChange: (value: string) => void; maxLength?: number; className?: string; rows?: number;
}) {
  return (
    <div className={className}>
      <SystemField label={label}>
        <textarea className="system-field__control" value={value} rows={rows} maxLength={maxLength} onChange={e => onChange(e.target.value)} />
      </SystemField>
    </div>
  );
}

/** A number typed in a text box: null when blank, NaN when not a number. */
export const parseNum = (text: string): number | null => (text.trim() === '' ? null : Number(text));
export const numText = (value: number | null | undefined) => (value == null ? '' : String(value));

/** The filter card: the filters, then Reset Filters (when onReset is given). */
export function FilterCard({ children, onReset }: { children: ReactNode; onReset?: () => void }) {
  return (
    <div className="system-card filter-card">
      <form className="pg-filter-grid" onSubmit={e => e.preventDefault()}>
        {children}
        {onReset && (
          <button type="button" className="btn--stroked" onClick={onReset}>
            <Icon name="filter_alt_off" />Reset Filters
          </button>
        )}
      </form>
    </div>
  );
}

/** The search box that usually starts a filter card (two columns wide). */
export function SearchField({ label, value, onChange, placeholder }: { label: string; value: string; onChange: (value: string) => void; placeholder?: string }) {
  return (
    <div className="pg-filter-grid__wide">
      <SystemField label={label} prefixIcon="search">
        <input className="system-field__control" placeholder={placeholder} value={value} onChange={e => onChange(e.target.value)} />
      </SystemField>
    </div>
  );
}

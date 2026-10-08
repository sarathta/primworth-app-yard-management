import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import SystemField from './SystemField';

export interface SystemMultiSelectOption {
  id: string | number;
  name: string;
}

interface SystemMultiSelectProps {
  label: string;
  options: SystemMultiSelectOption[];
  selectedIds: (string | number)[];
  onChange: (ids: (string | number)[]) => void;
  disabled?: boolean;
  loading?: boolean;
  error?: string;
  placeholder?: string;
  // Ids that must always stay selected — their chip has no remove button and the
  // dropdown ignores clicks that would toggle them off.
  lockedIds?: (string | number)[];
  // When true, renders a search box at the top of the dropdown panel that filters
  // the options by name. Useful when the option list is long.
  searchable?: boolean;
  searchPlaceholder?: string;
}

export default function SystemMultiSelect({
  label,
  options,
  selectedIds,
  onChange,
  disabled = false,
  loading = false,
  error,
  placeholder = 'Select options...',
  lockedIds = [],
  searchable = false,
  searchPlaceholder = 'Search...',
}: SystemMultiSelectProps): React.JSX.Element {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const [panelStyle, setPanelStyle] = useState<React.CSSProperties>({});

  // Close dropdown on click outside — the panel is portaled to <body> (see below),
  // so it lives outside rootRef in the DOM and must be checked separately.
  useEffect(() => {
    if (!open) return;
    function onDocClick(e: MouseEvent) {
      const target = e.target as Node;
      if (
        rootRef.current && !rootRef.current.contains(target) &&
        panelRef.current && !panelRef.current.contains(target)
      ) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, [open]);

  // The dropdown panel is rendered via a portal into <body> so it can escape the
  // modal's `overflow-y: auto` content area (which otherwise clips it). Since it's
  // no longer positioned via normal document flow, track the trigger's screen
  // position manually and keep it pinned while the page scrolls/resizes.
  useEffect(() => {
    if (!open) return;
    function updatePosition() {
      const rect = triggerRef.current?.getBoundingClientRect();
      if (!rect) return;
      setPanelStyle({
        position: 'fixed',
        top: rect.bottom + 4,
        left: rect.left,
        width: rect.width,
      });
    }
    updatePosition();
    window.addEventListener('scroll', updatePosition, true);
    window.addEventListener('resize', updatePosition);
    return () => {
      window.removeEventListener('scroll', updatePosition, true);
      window.removeEventListener('resize', updatePosition);
    };
  }, [open]);

  // Clear the search filter whenever the panel closes so it reopens clean.
  useEffect(() => {
    if (!open) setQuery('');
  }, [open]);

  const selectedOptions = options.filter(opt => selectedIds.includes(opt.id));
  const visibleOptions = searchable && query.trim()
    ? options.filter(opt => opt.name.toLowerCase().includes(query.trim().toLowerCase()))
    : options;

  function handleToggleOption(id: string | number) {
    if (selectedIds.includes(id)) {
      if (lockedIds.includes(id)) return;
      onChange(selectedIds.filter(x => x !== id));
    } else {
      onChange([...selectedIds, id]);
    }
  }

  function handleRemoveOption(id: string | number) {
    onChange(selectedIds.filter(x => x !== id));
  }

  return (
    <div ref={rootRef} className="system-multiselect-container" style={{ position: 'relative', width: '100%' }}>
      <SystemField label={label} error={error}>
        <div
          ref={triggerRef}
          className="multiselect-dropdown-trigger"
          tabIndex={disabled ? -1 : 0}
          onClick={() => !disabled && setOpen(o => !o)}
          onKeyDown={(e) => {
            if (disabled) return;
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault();
              setOpen(o => !o);
            } else if (e.key === 'Escape') {
              setOpen(false);
            }
          }}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            width: '100%',
            cursor: disabled ? 'not-allowed' : 'pointer',
            minHeight: '36px',
            padding: '4px 0',
            outline: 'none',
          }}
        >
          <div
            className="multiselect-chips-container"
            style={{
              display: 'flex',
              flexWrap: 'wrap',
              gap: '4px',
              alignItems: 'center',
              flex: 1,
            }}
          >
            {selectedOptions.length === 0 ? (
              <span className="ss-placeholder" style={{ color: 'var(--text-muted)', fontSize: '14px' }}>
                {loading ? 'Loading...' : placeholder}
              </span>
            ) : (
              selectedOptions.map(opt => (
                <span
                  key={opt.id}
                  className="multiselect-chip"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    background: 'var(--surface-container, rgba(0, 0, 0, 0.05))',
                    border: '1px solid var(--border)',
                    borderRadius: '4px',
                    padding: '2px 6px',
                    fontSize: '13px',
                    color: 'var(--text)',
                  }}
                >
                  {opt.name}
                  {!disabled && !lockedIds.includes(opt.id) && (
                    <button
                      type="button"
                      className="multiselect-chip__close"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleRemoveOption(opt.id);
                      }}
                      style={{
                        background: 'none',
                        border: 'none',
                        padding: 0,
                        cursor: 'pointer',
                        display: 'inline-flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: 'var(--text-muted)',
                      }}
                    >
                      <span className="material-symbols-outlined" style={{ fontSize: '14px' }}>close</span>
                    </button>
                  )}
                </span>
              ))
            )}
          </div>
          <span className="material-symbols-outlined ss-chevron" style={{ color: 'var(--text-muted)', marginLeft: '8px' }}>
            {open ? 'expand_less' : 'expand_more'}
          </span>
        </div>
      </SystemField>

      {open && createPortal(
        <div
          ref={panelRef}
          className="ss-panel"
          style={{
            ...panelStyle,
            zIndex: 2100,
            background: 'var(--surface-card)',
            border: '1px solid var(--border)',
            borderRadius: 'var(--radius-md)',
            boxShadow: '0 6px 18px rgba(0, 0, 0, 0.15)',
            maxHeight: '240px',
            overflowY: 'auto',
          }}
        >
          {searchable && !loading && options.length > 0 && (
            <div
              style={{
                position: 'sticky',
                top: 0,
                background: 'var(--surface-card)',
                padding: '8px',
                borderBottom: '1px solid var(--border)',
                zIndex: 1,
              }}
            >
              <input
                type="text"
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={searchPlaceholder}
                className="system-field__control"
                style={{ width: '100%', boxSizing: 'border-box' }}
                onClick={(e) => e.stopPropagation()}
              />
            </div>
          )}
          {loading && (
            <div style={{ padding: '12px', textAlign: 'center', color: 'var(--text-muted)' }}>
              Loading options...
            </div>
          )}
          {!loading && options.length === 0 && (
            <div style={{ padding: '12px', textAlign: 'center', color: 'var(--text-muted)' }}>
              No options available
            </div>
          )}
          {!loading && options.length > 0 && visibleOptions.length === 0 && (
            <div style={{ padding: '12px', textAlign: 'center', color: 'var(--text-muted)' }}>
              No matches found
            </div>
          )}
          {!loading && visibleOptions.map(opt => {
            const isSelected = selectedIds.includes(opt.id);
            return (
              <div
                key={opt.id}
                className={`ss-option ${isSelected ? 'ss-option--active' : ''}`}
                onClick={() => handleToggleOption(opt.id)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '10px 12px',
                  cursor: 'pointer',
                  fontSize: '13px',
                  background: isSelected ? 'rgba(25, 118, 210, 0.12)' : 'transparent',
                }}
              >
                <span>{opt.name}</span>
                {isSelected && (
                  <span className="material-symbols-outlined" style={{ fontSize: '16px', color: 'var(--primary)' }}>
                    check
                  </span>
                )}
              </div>
            );
          })}
        </div>,
        document.body
      )}
    </div>
  );
}

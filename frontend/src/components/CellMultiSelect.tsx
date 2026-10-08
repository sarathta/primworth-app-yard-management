import { useEffect, useLayoutEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { createPortal } from 'react-dom';

interface CellMultiSelectProps {
  options: string[];
  value: string[];
  onChange: (value: string[]) => void;
  disabled?: boolean;
  placeholder?: string;
  loading?: boolean;
}

function formatTriggerLabel(value: string[], placeholder: string): string {
  if (value.length === 0) return placeholder;
  if (value.length === 1) return value[0];
  if (value.length === 2) return value.join(', ');
  return `${value.slice(0, 2).join(', ')} +${value.length - 2}`;
}

export default function CellMultiSelect({
  options,
  value,
  onChange,
  disabled = false,
  placeholder = '— select columns —',
  loading = false,
}: CellMultiSelectProps): React.JSX.Element {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [panelStyle, setPanelStyle] = useState<CSSProperties>({});
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return options;
    return options.filter(o => o.toLowerCase().includes(q));
  }, [options, search]);

  function updatePanelPosition(): void {
    const trigger = triggerRef.current;
    if (!trigger) return;
    const rect = trigger.getBoundingClientRect();
    const panelHeight = panelRef.current?.offsetHeight ?? 280;
    const spaceBelow = window.innerHeight - rect.bottom;
    const openUp = spaceBelow < panelHeight && rect.top > spaceBelow;

    setPanelStyle({
      position: 'fixed',
      left: rect.left,
      width: Math.max(rect.width, 220),
      zIndex: 10000,
      ...(openUp
        ? { bottom: window.innerHeight - rect.top + 4 }
        : { top: rect.bottom + 4 }),
    });
  }

  useLayoutEffect(() => {
    if (!open) return;
    updatePanelPosition();
    const onResize = () => updatePanelPosition();
    window.addEventListener('resize', onResize);
    window.addEventListener('scroll', onResize, true);
    return () => {
      window.removeEventListener('resize', onResize);
      window.removeEventListener('scroll', onResize, true);
    };
  }, [open, filtered.length, value.length]);

  useEffect(() => {
    if (!open) return;
    function onDocClick(e: MouseEvent) {
      const target = e.target as Node;
      if (
        rootRef.current?.contains(target) ||
        panelRef.current?.contains(target)
      ) {
        return;
      }
      setOpen(false);
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false);
    }
    document.addEventListener('mousedown', onDocClick);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDocClick);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  useEffect(() => {
    if (open) {
      setSearch('');
      requestAnimationFrame(() => {
        updatePanelPosition();
        searchRef.current?.focus();
      });
    }
  }, [open]);

  function toggleOption(opt: string): void {
    const next = value.includes(opt)
      ? value.filter(v => v !== opt)
      : [...value, opt];
    onChange(next);
  }

  const label = loading ? 'Loading…' : formatTriggerLabel(value, placeholder);
  const isPlaceholder = !loading && value.length === 0;

  const panel = open && !loading && createPortal(
    <div className="cms-panel" ref={panelRef} style={panelStyle} role="listbox" aria-multiselectable="true">
      {options.length > 6 && (
        <div className="cms-search">
          <span className="material-symbols-outlined cms-search__icon">search</span>
          <input
            ref={searchRef}
            className="cms-search__input"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Search columns…"
            onKeyDown={e => { if (e.key === 'Escape') setOpen(false); }}
          />
        </div>
      )}

      <div className="cms-list">
        {options.length === 0 && (
          <div className="cms-empty">No columns available</div>
        )}
        {options.length > 0 && filtered.length === 0 && (
          <div className="cms-empty">No matches</div>
        )}
        {filtered.map(opt => {
          const selected = value.includes(opt);
          return (
            <button
              type="button"
              key={opt}
              className={'cms-option' + (selected ? ' cms-option--selected' : '')}
              onClick={() => toggleOption(opt)}
              role="option"
              aria-selected={selected}
            >
              <span className="material-symbols-outlined cms-option__check">
                {selected ? 'check' : ''}
              </span>
              <span className="cms-option__label font-code">{opt}</span>
            </button>
          );
        })}
      </div>

      {value.length > 0 && (
        <div className="cms-footer">
          <span>{value.length} selected</span>
          <button type="button" className="cms-clear" onClick={() => onChange([])}>
            Clear
          </button>
        </div>
      )}
    </div>,
    document.body,
  );

  return (
    <div className="cms-root" ref={rootRef}>
      <button
        ref={triggerRef}
        type="button"
        className={'cms-trigger' + (open ? ' cms-trigger--open' : '')}
        disabled={disabled || loading}
        onClick={() => setOpen(o => !o)}
        aria-haspopup="listbox"
        aria-expanded={open}
        title={value.length > 0 ? value.join(', ') : undefined}
      >
        <span className={'cms-trigger__text' + (isPlaceholder ? ' cms-placeholder' : '')}>
          {label}
        </span>
        <span className="material-symbols-outlined cms-trigger__chevron">
          {open ? 'expand_less' : 'expand_more'}
        </span>
      </button>
      {panel}
    </div>
  );
}

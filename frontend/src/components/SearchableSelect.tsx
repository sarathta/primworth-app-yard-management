import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';

export interface SearchableOption {
  value: string;
  label: string;
  badge?: string;
}

interface Props {
  value: string;
  options: SearchableOption[];
  onChange: (value: string) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  loading?: boolean;
  disabled?: boolean;
  // Render the dropdown menu in a portal on document.body (position: fixed) instead of
  // absolutely inside the trigger. Use this when the select lives inside a scroll/overflow
  // container (e.g. an editable grid cell) that would otherwise clip the menu. Default off
  // keeps the existing inline behavior for every other usage.
  menuPortal?: boolean;
  // Override the trigger's base class (the part that styles height/border/padding). Defaults
  // to the app's field style; callers can pass a shared control class so the trigger matches
  // sibling inputs (e.g. the editable grid row). `ss-trigger` is always kept for layout.
  triggerClassName?: string;
}

/**
 * Lightweight searchable single-select dropdown.
 * - Type to filter options.
 * - List scrolls automatically once it grows past ~5 rows (capped max-height).
 * - Closes on outside click or Escape.
 */
export default function SearchableSelect({
  value,
  options,
  onChange,
  placeholder = 'Select…',
  searchPlaceholder = 'Search…',
  loading = false,
  disabled = false,
  menuPortal = false,
  triggerClassName = 'wm-field__input',
}: Props): React.JSX.Element {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const rootRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);

  // Fixed-position box for the portalled menu, tracked from the trigger's rect so it stays
  // anchored while the page scrolls/resizes. Only used when menuPortal is set.
  const [menuBox, setMenuBox] = useState<{ top: number; left: number; width: number }>(
    { top: 0, left: 0, width: 0 });

  const selected = options.find(o => o.value === value);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return options;
    return options.filter(o => o.label.toLowerCase().includes(q));
  }, [options, search]);

  // Position the portalled menu directly under the trigger, matching its width.
  const reposition = (): void => {
    const el = rootRef.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    setMenuBox({ top: r.bottom + 4, left: r.left, width: r.width });
  };

  // Measure before paint when opening the portalled menu (avoids a first-frame jump), and
  // keep it anchored to the trigger while the page scrolls or the window resizes.
  useLayoutEffect(() => {
    if (!open || !menuPortal) return;
    reposition();
    window.addEventListener('scroll', reposition, true); // capture: catch inner scrollers too
    window.addEventListener('resize', reposition);
    return () => {
      window.removeEventListener('scroll', reposition, true);
      window.removeEventListener('resize', reposition);
    };
  }, [open, menuPortal]);

  // Close on outside click. The portalled menu lives outside rootRef, so treat clicks in it
  // (search box / options) as inside too.
  useEffect(() => {
    if (!open) return;
    function onDocClick(e: MouseEvent) {
      const t = e.target as Node;
      if (rootRef.current?.contains(t)) return;
      if (panelRef.current?.contains(t)) return;
      setOpen(false);
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, [open]);

  // Focus the search box when opened
  useEffect(() => {
    if (open) {
      setSearch('');
      // defer so the input is mounted
      requestAnimationFrame(() => searchRef.current?.focus());
    }
  }, [open]);

  function pick(opt: SearchableOption) {
    onChange(opt.value);
    setOpen(false);
  }

  return (
    <div className="ss-root" ref={rootRef}>
      <button
        type="button"
        className={`${triggerClassName} ss-trigger`}
        disabled={disabled}
        onClick={() => setOpen(o => !o)}
        aria-haspopup="listbox"
        aria-expanded={open}
      >
        <span className={selected ? 'ss-value' : 'ss-placeholder'}>
          {selected ? selected.label : placeholder}
        </span>
        <span className="material-symbols-outlined ss-chevron">
          {open ? 'expand_less' : 'expand_more'}
        </span>
      </button>

      {open && (() => {
        const panel = (
          <div
            ref={panelRef}
            className={`ss-panel${menuPortal ? ' ss-panel--portal' : ''}`}
            role="listbox"
            style={menuPortal
              ? { position: 'fixed', top: menuBox.top, left: menuBox.left, width: menuBox.width, right: 'auto' }
              : undefined}
          >
            <div className="ss-search">
              <span className="material-symbols-outlined ss-search__icon">search</span>
              <input
                ref={searchRef}
                className="ss-search__input"
                value={search}
                onChange={e => setSearch(e.target.value)}
                placeholder={searchPlaceholder}
                onKeyDown={e => { if (e.key === 'Escape') setOpen(false); }}
              />
            </div>

            <div className="ss-list">
              {loading && <div className="ss-empty">Loading…</div>}

              {!loading && filtered.length === 0 && (
                <div className="ss-empty">No matches</div>
              )}

              {!loading && filtered.map(opt => (
                <button
                  type="button"
                  key={opt.value}
                  className={'ss-option' + (opt.value === value ? ' ss-option--active' : '')}
                  onClick={() => pick(opt)}
                  role="option"
                  aria-selected={opt.value === value}
                >
                  <span className="ss-option__label">{opt.label}</span>
                  {opt.badge && <span className="ss-option__badge">{opt.badge}</span>}
                </button>
              ))}
            </div>
          </div>
        );
        // Portalled to document.body so no ancestor overflow/stacking context can clip it.
        return menuPortal ? createPortal(panel, document.body) : panel;
      })()}
    </div>
  );
}

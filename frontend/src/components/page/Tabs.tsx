// A tab strip for pages with several sections (the platform has no shared one; this follows its
// tracking pages). Render the active section yourself, so each loads when it is opened.
import { Icon } from './PageHeader';
import './page.css';

export function Tabs<K extends string>({ tabs, active, onChange }: {
  tabs: { key: K; label: string; icon: string }[];
  active: K;
  onChange: (key: K) => void;
}) {
  return (
    <div className="pg-tabs" role="tablist">
      {tabs.map(t => (
        <button key={t.key} type="button" role="tab" aria-selected={t.key === active}
                className={`pg-tab${t.key === active ? ' pg-tab--active' : ''}`} onClick={() => onChange(t.key)}>
          <Icon name={t.icon} />{t.label}
        </button>
      ))}
    </div>
  );
}

import { useEffect, useState } from 'react';
import { Flame } from 'lucide-react';
import { BASE_URL } from '../../../../api';
import { symsFetch } from '../symsApi';
import { hasPermission } from '../../../../auth';

/**
 * One plant value, read from the tag hub rather than from a connection Slab Yard owns.
 *
 * It asks for a single tag by name and shows the value, how old it is, and whether it can
 * be trusted. It does not know which connection feeds that tag, which protocol, or whether
 * the source is a real PLC or a simulator; swapping one for the other changes nothing here.
 *
 * Which tag is a per-facility setting now. It used to be the literal string
 * "BRM1 / Furnace / Zone1 / Temperature" compiled into this file, which meant every
 * customer's dashboard asked for one particular mill's furnace and showed a dead badge
 * when it was not theirs. It is read from the yard's own configuration, and when nothing
 * is configured the badge does not appear at all — an empty slot on a dashboard says
 * nothing useful, and a broken one says something untrue.
 *
 * It also disappears for a role without Tag Hub read permission, rather than sitting there
 * permanently reporting the hub unreachable when the hub is fine and the answer was simply
 * no.
 */
interface TagReading {
  path: string;
  value: number | string | boolean | null;
  timestamp: string;
  age: number;
  quality: 'Good' | 'Stale' | 'Bad' | 'Unknown';
}

/** A dashboard badge, not a control loop — the hub's own read loop runs far faster. */
const POLL_MS = 5000;

export function PlantLinkBadge(): React.JSX.Element | null {
  const [tag, setTag] = useState<{ path: string; label: string } | null>(null);
  const [reading, setReading] = useState<TagReading | null>(null);
  const [reachable, setReachable] = useState(true);

  const allowed = hasPermission('THVW');

  // Which tag, from the yard's configuration.
  useEffect(() => {
    if (!allowed) return;
    let cancelled = false;

    (async () => {
      try {
        const res = await symsFetch('http://localhost:4000/api/config');
        if (!res.ok) return;
        const rows: Array<{ key: string; value: string }> = await res.json();
        const map = Object.fromEntries(rows.map(r => [r.key, r.value]));
        const path = (map.dashboard_tag_path ?? '').trim();
        if (!cancelled && path) {
          setTag({ path, label: (map.dashboard_tag_label ?? '').trim() || path.split('/').pop()!.trim() });
        }
      } catch { /* no badge is the right answer when we cannot tell which tag */ }
    })();

    return () => { cancelled = true; };
  }, [allowed]);

  useEffect(() => {
    if (!tag) return;
    let cancelled = false;

    const read = async () => {
      try {
        const res = await symsFetch(`${BASE_URL}/api/TagHub/Tags?path=${encodeURIComponent(tag.path)}`);
        if (!res.ok) { if (!cancelled) setReachable(false); return; }
        const body = await res.json();
        const first: TagReading | undefined = body?.data?.[0];
        if (!cancelled) { setReachable(true); setReading(first ?? null); }
      } catch {
        if (!cancelled) setReachable(false);
      }
    };

    read();
    const timer = setInterval(read, POLL_MS);
    return () => { cancelled = true; clearInterval(timer); };
  }, [tag]);

  if (!allowed || !tag) return null;

  const quality = reading?.quality ?? 'Unknown';
  const tone =
    !reachable ? 'text-secondary border-[#333]' :
    quality === 'Good' ? 'text-success border-success' :
    quality === 'Stale' ? 'text-[#FF8C00] border-[#FF8C00]' :
    quality === 'Bad' ? 'text-[#DC143C] border-[#DC143C]' :
    'text-secondary border-[#333]';

  const value = typeof reading?.value === 'number' ? reading.value.toFixed(1) : '—';
  const age = reading ? (reading.age < 60 ? `${reading.age.toFixed(0)} s` : `${Math.floor(reading.age / 60)} min`) : '';

  return (
    <div
      className={`ml-4 flex items-center text-[10px] font-normal border px-2 py-1 rounded tracking-widest ${tone}`}
      title={`${tag.path} — read from the tag hub, not from a connection Slab Yard owns`}
    >
      <Flame size={12} className="mr-1.5" />
      <span className="uppercase">{tag.label}</span>
      <span className="ml-2 font-bold">{value}</span>
      {reading && <span className="ml-2 opacity-70">{age} · {quality}</span>}
      {!reachable && <span className="ml-2 opacity-70">hub unreachable</span>}
    </div>
  );
}

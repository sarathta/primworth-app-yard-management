import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useProjectContext } from '../../../contexts/ProjectContext';
import { useYardSelection, type SelectedYard } from '../../../contexts/YardSelectionContext';
import { getYardTypes } from '../../../services/yard';
import type { YardTypeOption } from '../../../types/yard';

/**
 * First screen after login. Every yard in sp_yard_type is offered together; the ones
 * confirmed here each become a menu group on the left.
 */
export default function SelectYardsNotice(): React.JSX.Element {
  const navigate = useNavigate();
  const { subProjectId, subProjects, projects, setSubProject } = useProjectContext();
  const { selected, setSelected } = useYardSelection();
  const [types, setTypes] = useState<YardTypeOption[]>([]);
  const [picked, setPicked] = useState<number[]>(() => selected.map(y => y.yard_type_id));
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (subProjectId || subProjects.length === 0) return;
    const first = subProjects[0];
    setSubProject(String(first.id), first.name, 'Yard Management');
  }, [subProjectId, subProjects, setSubProject]);

  useEffect(() => {
    if (!subProjectId) {
      if (projects.length > 0 && subProjects.length === 0) setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    getYardTypes()
      .then(res => {
        if (cancelled) return;
        if (!res.success || !res.data) {
          setTypes([]);
          setError(res.message || 'Could not load yards.');
        } else {
          setError('');
          setTypes([...res.data].sort((a, b) => a.yard_type_id - b.yard_type_id));
        }
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [subProjectId, projects.length, subProjects.length]);

  const toggle = (id: number) => {
    setPicked(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]);
  };

  const open = () => {
    const byId = new Map(types.map(t => [t.yard_type_id, t]));
    const chosen: SelectedYard[] = picked.flatMap(id => {
      const type = byId.get(id);
      return type ? [{
        yard_type_id: type.yard_type_id,
        yard_type_name: type.yard_type_name,
        description: type.description,
      }] : [];
    });
    if (chosen.length === 0) return;
    setSelected(chosen);
    navigate(`/yard?yardTypeId=${chosen[0].yard_type_id}`, { replace: true });
  };

  return (
    <div className="system-empty-state">
      <span className="material-symbols-outlined">warehouse</span>
      <h3>Select yards</h3>
      <p style={{ maxWidth: '520px' }}>
        Choose one or more yards. Each yard you open gets its own menu on the left.
      </p>
      {types.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', width: 'min(420px, 100%)', margin: '8px 0' }}>
          {types.map(type => {
            const on = picked.includes(type.yard_type_id);
            return (
              <button
                key={type.yard_type_id}
                type="button"
                className={on ? 'btn btn--primary' : 'btn btn--secondary'}
                onClick={() => toggle(type.yard_type_id)}
                aria-pressed={on}
                style={{ width: '100%', justifyContent: 'flex-start', textAlign: 'left' }}
              >
                <span className="material-symbols-outlined">{on ? 'check_box' : 'check_box_outline_blank'}</span>
                <span style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start' }}>
                  <span>{type.yard_type_name}</span>
                  {type.description && <span style={{ fontWeight: 500, opacity: 0.85 }}>{type.description}</span>}
                </span>
              </button>
            );
          })}
        </div>
      )}
      {types.length > 0 && (
        <button type="button" className="btn btn--primary" disabled={picked.length === 0} onClick={open}
                style={picked.length === 0 ? { opacity: 0.45, cursor: 'not-allowed' } : undefined}>
          Open selected yards
        </button>
      )}
      {loading && <p>Loading yards...</p>}
      {!loading && error && <p>{error}</p>}
      {!loading && !error && !subProjectId && <p>No yard is available for this project yet.</p>}
      {!loading && !error && subProjectId && types.length === 0 && <p>No yards are set up yet.</p>}
    </div>
  );
}

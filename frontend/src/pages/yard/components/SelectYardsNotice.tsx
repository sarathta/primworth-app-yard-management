import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useYardSelection, type SelectedYard } from '../../../contexts/YardSelectionContext';

/**
 * Shown when the signed-in account has more than one yard. Only those yards are offered;
 * the ones confirmed here each become a menu group on the left.
 */
export default function SelectYardsNotice(): React.JSX.Element {
  const navigate = useNavigate();
  const { assigned, selected, setSelected } = useYardSelection();
  const [picked, setPicked] = useState<number[]>(() => selected.map(y => y.yard_type_id));

  const toggle = (id: number) => {
    setPicked(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]);
  };

  const open = () => {
    const chosen: SelectedYard[] = assigned.filter(yard => picked.includes(yard.yard_type_id));
    if (chosen.length === 0) return;
    setSelected(chosen);
    navigate(`/yard?yardTypeId=${chosen[0].yard_type_id}`, { replace: true });
  };

  return (
    <div className="system-empty-state">
      <span className="material-symbols-outlined">warehouse</span>
      <h3>Select yards</h3>
      {assigned.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', width: 'min(420px, 100%)', margin: '8px 0' }}>
          {assigned.map(type => {
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
      {assigned.length > 0 && (
        <button type="button" className="btn btn--primary" disabled={picked.length === 0} onClick={open}
                style={picked.length === 0 ? { opacity: 0.45, cursor: 'not-allowed' } : undefined}>
          Open selected yards
        </button>
      )}
    </div>
  );
}

import { useEffect, useState } from 'react';
import { useProjectContext } from '../../../contexts/ProjectContext';
import { getFacilityTypes } from '../../../services/yard';
import { clearYardMaterial, writeFacilityChoice } from '../yardMaterial';
import type { SubprojectOption } from '../../../types/system';

interface FacilityType {
  facility_type_id: number;
  facility_type_name: string;
  description?: string | null;
}

/**
 * First step into the yard: Steel Melt Shop, Rolling Mill, Hot Strip Mill, Plate Mill, and
 * whatever else gm_facility_type holds. The material list after this is the rows whose
 * facility_type_id matches.
 *
 * The yard tables still live on a sub-project. A sub-project of the chosen type is used when
 * one exists; otherwise the plant's only sub-project is used, which is where a single yard
 * keeps every material tagged by facility type.
 */
export default function SelectFacilityNotice({ onChosen }: { onChosen: () => void }): React.JSX.Element {
  const { subProjectId, subProjects, setSubProject } = useProjectContext();
  const [types, setTypes] = useState<FacilityType[]>([]);
  const [pending, setPending] = useState<FacilityType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  // The facility-type list lives in the yard schema, next to the materials. One sub-project
  // is selected for that read; the buttons themselves are the facility types.
  useEffect(() => {
    if (subProjectId || subProjects.length !== 1) return;
    const only = subProjects[0];
    setSubProject(String(only.id), only.name, 'Yard Management');
  }, [subProjectId, subProjects, setSubProject]);

  useEffect(() => {
    if (!subProjectId) {
      if (subProjects.length === 0) setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    getFacilityTypes()
      .then(res => {
        if (cancelled) return;
        if (!res.success || !res.data) {
          setTypes([]);
          setError(res.message || 'Could not load facilities.');
        } else {
          setError('');
          setTypes([...res.data].sort((a, b) => a.facility_type_id - b.facility_type_id));
        }
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [subProjectId, subProjects.length]);

  const open = (type: FacilityType, sub: SubprojectOption) => {
    clearYardMaterial();
    writeFacilityChoice({
      facility_type_id: type.facility_type_id,
      facility_type_name: type.facility_type_name,
      subProjectId: String(sub.id),
    });
    setSubProject(String(sub.id), sub.name, 'Yard Management');
    onChosen();
  };

  const chooseType = (type: FacilityType) => {
    const matches = subProjects.filter(s => s.facility_type_id === type.facility_type_id);
    if (matches.length === 1) { open(type, matches[0]); return; }
    if (matches.length === 0 && subProjects.length === 1) { open(type, subProjects[0]); return; }
    if (matches.length > 1) { setPending(type); return; }
    setError(`${type.facility_type_name} has no yard to open.`);
  };

  return (
    <div className="system-empty-state">
      <span className="material-symbols-outlined">factory</span>
      <h3>Select a facility</h3>
      <p style={{ maxWidth: '520px' }}>
        {pending
          ? `${pending.facility_type_name} has more than one yard. Choose which one to open.`
          : 'Choose a facility. The next step is the material that facility stores.'}
      </p>
      {!pending && types.length > 0 && (
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'center', margin: '8px 0' }}>
          {types.map(type => (
            <button key={type.facility_type_id} type="button" className="btn btn--primary"
                    onClick={() => chooseType(type)}
                    style={{ flexDirection: 'column', alignItems: 'flex-start', textAlign: 'left' }}>
              <span>{type.facility_type_name}</span>
              {type.description && <span style={{ fontWeight: 500, opacity: 0.85 }}>{type.description}</span>}
            </button>
          ))}
        </div>
      )}
      {pending && (
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'center', margin: '8px 0' }}>
          {subProjects.filter(s => s.facility_type_id === pending.facility_type_id).map(sp => (
            <button key={sp.id} type="button" className="btn btn--primary" onClick={() => open(pending, sp)}>
              {sp.name}
            </button>
          ))}
          <button type="button" className="btn btn--secondary" onClick={() => setPending(null)}>Back</button>
        </div>
      )}
      {loading && <p>Loading facilities...</p>}
      {!loading && error && <p>{error}</p>}
      {!loading && !error && types.length === 0 && <p>No facilities are set up yet.</p>}
    </div>
  );
}

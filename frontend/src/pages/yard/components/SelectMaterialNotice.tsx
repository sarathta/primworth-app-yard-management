import { useEffect, useState } from 'react';
import { useProjectContext } from '../../../contexts/ProjectContext';
import { getMaterialTypes } from '../../../services/yard';
import { clearFacilityChoice } from '../yardMaterial';
import type { MaterialType } from '../../../types/yard';

/**
 * Shown once a facility is selected and before the yard overview.
 *
 * sp_yard_material_type rows carry the facility type they belong to. The list here is already
 * limited to the facility that was just chosen, so a melt shop offers its materials and a
 * plate mill offers its own.
 */
export default function SelectMaterialNotice({
  facilityName,
  facilityTypeId,
  facilityTypeName,
  onSelect,
  onChangeFacility,
}: {
  facilityName: string;
  facilityTypeId?: number | null;
  facilityTypeName?: string | null;
  onSelect: (material: MaterialType) => void;
  onChangeFacility?: () => void;
}): React.JSX.Element {
  const { setSubProject } = useProjectContext();
  const [materials, setMaterials] = useState<MaterialType[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getMaterialTypes(false, facilityTypeId).then(res => {
      if (cancelled) return;
      if (!res.success || !res.data) {
        setMaterials([]);
        setError(res.message || 'Could not load materials for this facility.');
      } else {
        setError('');
        setMaterials([...res.data].sort((a, b) => a.material_type_id - b.material_type_id));
      }
      setLoading(false);
    });
    return () => { cancelled = true; };
  }, [attempt, facilityTypeId]);

  return (
    <div className="system-empty-state">
      <span className="material-symbols-outlined">category</span>
      <h3>Select a material</h3>
      <p style={{ maxWidth: '520px' }}>
        {facilityTypeName || facilityName || 'This facility'} is selected.
        {' '}Choose the material this facility stores to open its overview.
      </p>
      {materials.length > 0 && (
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'center', margin: '8px 0' }}>
          {materials.map(material => (
            <button
              key={material.material_type_id}
              type="button"
              className="btn btn--primary"
              onClick={() => onSelect(material)}
            >
              {material.singular}
            </button>
          ))}
        </div>
      )}
      {loading && <p>Loading materials...</p>}
      {!loading && error && (
        <>
          <p>{error}</p>
          <button type="button" className="btn btn--secondary" onClick={() => setAttempt(n => n + 1)}>
            Try again
          </button>
        </>
      )}
      {!loading && !error && materials.length === 0 && (
        <p>No materials are set up for this facility.</p>
      )}
      <button type="button" className="btn btn--secondary" onClick={() => {
        clearFacilityChoice();
        setSubProject('', '');
        onChangeFacility?.();
      }}>
        Change facility
      </button>
    </div>
  );
}

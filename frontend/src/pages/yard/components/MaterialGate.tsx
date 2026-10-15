import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { getMaterialTypes } from '../../../services/yard';
import { setSymsFacilityType, setSymsMaterial } from '../msyms/symsApi';
import type { MaterialType } from '../../../types/yard';
import SelectMaterialNotice from './SelectMaterialNotice';
import { clearFacilityChoice, clearYardMaterial, readYardMaterial, writeYardMaterial, type YardMaterialChoice } from '../yardMaterial';

interface YardMaterialContextValue {
  material: YardMaterialChoice;
  changeMaterial: () => void;
}

const YardMaterialContext = createContext<YardMaterialContextValue | null>(null);

export function useYardMaterial(): YardMaterialContextValue {
  const ctx = useContext(YardMaterialContext);
  if (!ctx) throw new Error('useYardMaterial must be used after a material is chosen');
  return ctx;
}

/** Screens stay mounted under the yard that was just opened. `onChangeYards` returns to the picker. */
export function YardMaterialScope({
  material,
  onChangeYards,
  children,
}: {
  material: YardMaterialChoice;
  onChangeYards: () => void;
  children: ReactNode;
}): React.JSX.Element {
  return (
    <YardMaterialContext.Provider value={{ material, changeMaterial: onChangeYards }}>
      {children}
    </YardMaterialContext.Provider>
  );
}

/**
 * The step between "facility chosen" and the yard screens.
 *
 * The choice is written during render, before any child effect fetches, for the same reason
 * the facility is: a child effect runs after this render, and the overview request has to
 * already name the material or it comes back with every material mixed together.
 */
function choiceFrom(subProjectId: string, material: MaterialType): YardMaterialChoice {
  return {
    subProjectId,
    type_key: material.type_key,
    singular: material.singular,
    plural: material.plural,
    id_label: material.id_label,
  };
}

export default function MaterialGate({
  subProjectId,
  facilityName,
  facilityTypeId,
  facilityTypeName,
  onChangeFacility,
  children,
}: {
  subProjectId: string;
  facilityName: string;
  facilityTypeId: number;
  facilityTypeName?: string | null;
  onChangeFacility: () => void;
  children: ReactNode;
}): React.JSX.Element {
  const [selected, setSelected] = useState<YardMaterialChoice | null>(null);
  const [checked, setChecked] = useState(false);

  // A material remembered from an earlier visit is kept only when it still belongs to this
  // facility type. Switching from a melt shop to a plate mill must not reopen the slab yard.
  useEffect(() => {
    let cancelled = false;
    setChecked(false);
    setSelected(null);
    const stored = readYardMaterial(subProjectId);
    getMaterialTypes(false, facilityTypeId).then(res => {
      if (cancelled) return;
      const match = stored && res.success ? res.data?.find(m => m.type_key === stored.type_key) : undefined;
      if (match) setSelected(choiceFrom(subProjectId, match));
      else {
        if (stored) clearYardMaterial();
        setSelected(null);
      }
      setChecked(true);
    });
    return () => { cancelled = true; };
  }, [subProjectId, facilityTypeId]);

  setSymsMaterial(selected?.type_key ?? '');
  setSymsFacilityType(facilityTypeId);

  if (!checked) {
    return (
      <div className="slab-yard">
        <div className="system-empty-state"><p>Loading materials...</p></div>
      </div>
    );
  }

  if (!selected) {
    return (
      <div className="slab-yard">
        <SelectMaterialNotice
          facilityName={facilityName}
          facilityTypeId={facilityTypeId}
          facilityTypeName={facilityTypeName}
          onChangeFacility={() => {
            clearYardMaterial();
            clearFacilityChoice();
            setSymsFacilityType(null);
            setSymsMaterial('');
            onChangeFacility();
          }}
          onSelect={(material: MaterialType) => {
            const choice = choiceFrom(subProjectId, material);
            writeYardMaterial(choice);
            setSelected(choice);
          }}
        />
      </div>
    );
  }

  return (
    <YardMaterialContext.Provider
      value={{
        material: selected,
        changeMaterial: () => {
          clearYardMaterial();
          setSymsMaterial('');
          setSelected(null);
        },
      }}
    >
      <div key={selected.type_key} style={{ display: 'contents' }}>{children}</div>
    </YardMaterialContext.Provider>
  );
}

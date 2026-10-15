// The material chosen after a facility. Kept beside the facility selection, and only for that
// facility: switching plant must not reopen the previous plant's slab yard as if it were coils.

const FACILITY_KEY = 'pw_yardFacilityType';
const KEY = 'pw_yardMaterial';

export interface YardFacilityChoice {
  facility_type_id: number;
  facility_type_name: string;
  subProjectId: string;
}

export function readFacilityChoice(): YardFacilityChoice | null {
  try {
    const raw = sessionStorage.getItem(FACILITY_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<YardFacilityChoice>;
    if (!parsed.facility_type_id || !parsed.subProjectId) return null;
    return {
      facility_type_id: parsed.facility_type_id,
      facility_type_name: parsed.facility_type_name || '',
      subProjectId: parsed.subProjectId,
    };
  } catch {
    return null;
  }
}

export function writeFacilityChoice(choice: YardFacilityChoice): void {
  sessionStorage.setItem(FACILITY_KEY, JSON.stringify(choice));
}

export function clearFacilityChoice(): void {
  sessionStorage.removeItem(FACILITY_KEY);
}

export interface YardMaterialChoice {
  subProjectId: string;
  type_key: string;
  singular: string;
  plural: string;
  id_label: string;
}

export function readYardMaterial(subProjectId: string): YardMaterialChoice | null {
  if (!subProjectId) return null;
  try {
    const raw = sessionStorage.getItem(KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<YardMaterialChoice>;
    if (parsed.subProjectId !== subProjectId || !parsed.type_key) return null;
    return {
      subProjectId,
      type_key: parsed.type_key,
      singular: parsed.singular || parsed.type_key,
      plural: parsed.plural || parsed.singular || parsed.type_key,
      id_label: parsed.id_label || parsed.singular || parsed.type_key,
    };
  } catch {
    return null;
  }
}

export function writeYardMaterial(choice: YardMaterialChoice): void {
  sessionStorage.setItem(KEY, JSON.stringify(choice));
}

export function clearYardMaterial(): void {
  sessionStorage.removeItem(KEY);
}

/** Words the screens use for the open yard. The API is scoped by yard type, not by this key. */
export function materialFromYard(yardName: string): YardMaterialChoice {
  const name = yardName.trim();
  const lower = name.toLowerCase();
  const base = name.replace(/\s*yard\s*$/i, '').trim() || name;
  let type_key = '';
  let plural = base.endsWith('s') ? base : `${base}s`;
  if (lower.includes('coil')) { type_key = 'COIL'; plural = 'Coils'; }
  else if (lower.includes('billet')) { type_key = 'BILLET'; plural = 'Billets'; }
  else if (lower.includes('tmt')) { type_key = 'TMT_BUNDLE'; plural = 'Bundles'; }
  else if (lower.includes('wire')) { type_key = 'WIRE_ROD'; plural = 'Wire Rods'; }
  else if (lower.includes('plate')) { type_key = 'PLATE'; plural = 'Plates'; }
  else if (lower.includes('slab')) { type_key = 'SLAB'; plural = 'Slabs'; }
  return {
    subProjectId: '',
    type_key,
    singular: base,
    plural,
    id_label: `${base} ID`,
  };
}

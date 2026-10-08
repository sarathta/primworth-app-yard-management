// Shapes returned by api/yard/*. Field names are the API's snake_case.

export type StoragePurpose = 'STORAGE' | 'REWORK' | 'DISPATCH' | 'SHUFFLING';
export type StorageStyle = 'STACK' | 'RACK' | 'FLOOR' | 'PYRAMID';

/** One field a material carries, as declared on its material type. */
export interface AttributeDef {
  key: string;
  label: string;
  type: 'number' | 'integer' | 'text' | 'datetime' | 'boolean';
  unit?: string;
  required?: boolean;
}

export interface MaterialType {
  material_type_id: number;
  type_key: string;
  singular: string;
  plural: string;
  id_label: string;
  position_label: string;
  /** JSON text of AttributeDef[]; parse with parseAttributeDefs. */
  attribute_defs: string;
  rule_set_key: string | null;
  is_active: boolean;
}

export function parseAttributeDefs(raw: string): AttributeDef[] {
  try {
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? (parsed as AttributeDef[]).filter(d => typeof d.key === 'string') : [];
  } catch {
    return [];
  }
}

export interface Zone {
  zone_id: number;
  zone_name: string;
  zone_purpose: StoragePurpose;
  storage_style: StorageStyle;
  is_active: boolean;
  position_count: number;
  total_capacity: number;
  occupied: number;
  /** Comma-joined type keys the area accepts. */
  accepted_types: string | null;
}

export interface ZoneInput {
  zone_name: string;
  zone_purpose: StoragePurpose;
  storage_style: StorageStyle;
  material_type_keys: string[];
  rows: number;
  cols: number;
  capacity_limit: number;
}

export interface Position {
  position_id: number;
  zone_id: number;
  row_index: number;
  col_index: number;
  capacity_limit: number;
  occupied: number;
}

export interface Item {
  item_id: number;
  item_code: string;
  heat_number: string | null;
  material_type_id: number;
  grade_code: string | null;
  weight_kg: number | null;
  /** JSON text of the typed attribute bag. */
  attributes: string;
  item_status: 'PENDING' | 'READY' | 'HOLD' | 'REWORK' | 'SCRAPPED' | 'DISPATCHED';
  quality_status: 'PENDING' | 'APPROVED' | 'LOCKED_QA';
  zone_id: number | null;
  position_id: number | null;
  stack_index: number | null;
  arrived_on: string;
  type_key: string | null;
  zone_name: string | null;
  storage_style: StorageStyle | null;
  row_index: number | null;
  col_index: number | null;
}

export interface ItemInput {
  item_code: string;
  heat_number?: string | null;
  material_type_key?: string | null;
  grade_code?: string | null;
  weight_kg?: number | null;
  attributes?: Record<string, unknown>;
  allocate?: boolean;
}

export interface AllocationResult {
  item_id: number;
  allocated: boolean;
  work_order_id: number | null;
  destination: string | null;
  held: boolean;
  reason: string | null;
}

export interface WorkOrder {
  work_order_id: number;
  order_type: 'INBOUND' | 'MOVE' | 'DIGGING' | 'REWORK' | 'DISPATCH' | 'AUDIT';
  item_id: number;
  order_status: 'PENDING' | 'IN_PROGRESS' | 'COMPLETED' | 'FLAGGED';
  flag_reason: string | null;
  completed_at: string | null;
  created_on: string;
  item_code: string | null;
  heat_number: string | null;
  type_key: string | null;
  quality_status: string | null;
  source_zone_name: string | null;
  destination_zone_name: string | null;
  source_position: string | null;
  destination_position: string | null;
}

export interface Movement {
  movement_log_id: number;
  item_id: number;
  item_code: string | null;
  heat_number: string | null;
  from_zone_name: string | null;
  to_zone_name: string | null;
  from_position: string | null;
  to_position: string | null;
  order_type: string | null;
  performed_name: string | null;
  log_timestamp: string;
}

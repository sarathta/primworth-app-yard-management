// Every API call Yard Management makes, in one file, through the platform's client.
//
// authGet/authPost/authPut/authDelete attach the bearer token and the X-Project-Id /
// X-Sub-Project-Id headers from the current selection, count the request in the global loader
// and sign the user out on a 401. Nothing here builds a header or a URL by hand.
import { BASE_URL, authGet, authPost, authPut, authDelete } from '../api';
import type { ApiResponse } from '../types';
import type {
  AllocationResult, Item, ItemInput, MaterialType, Movement, Position, WorkOrder, YardTypeOption, Zone, ZoneInput,
} from '../types/yard';

const BASE = `${BASE_URL}/api/yard`;

async function attempt<T>(call: () => Promise<ApiResponse<T>>): Promise<ApiResponse<T>> {
  try { return await call(); }
  catch (err) { return { success: false, message: messageFrom(err), data: null }; }
}

// The client throws `new Error(bodyText)`; the body is the platform envelope when the API produced it.
function messageFrom(err: unknown): string {
  const text = err instanceof Error ? err.message : String(err);
  try {
    const parsed = JSON.parse(text) as Partial<ApiResponse<unknown>>;
    if (typeof parsed.message === 'string') return parsed.message;
  } catch { /* not JSON */ }
  return text || 'Request failed.';
}

// ── Layout and configuration ──────────────────────────────────────────────────
export const getFacilityTypes = () =>
  attempt(() => authGet<ApiResponse<Array<{ facility_type_id: number; facility_type_name: string; description?: string | null }>>>(`${BASE}/zones/facility-types`));
export const getYardTypes = () =>
  attempt(() => authGet<ApiResponse<YardTypeOption[]>>(`${BASE}/zones/yard-types`));
export const getMaterialTypes = (includeInactive = false, facilityTypeId?: number | null) =>
  attempt(() => authGet<ApiResponse<MaterialType[]>>(`${BASE}/zones/material-types`, {
    includeInactive,
    facilityTypeId: facilityTypeId && facilityTypeId > 0 ? facilityTypeId : undefined,
  }));
export const getZones = () => attempt(() => authGet<ApiResponse<Zone[]>>(`${BASE}/zones`));
export const getPositions = (zoneId: number) => attempt(() => authGet<ApiResponse<Position[]>>(`${BASE}/zones/${zoneId}/positions`));
export const createZone = (input: ZoneInput) => attempt(() => authPost<ApiResponse<number>>(`${BASE}/zones`, input));
export const deleteZone = (zoneId: number) => attempt(() => authDelete<ApiResponse<boolean>>(`${BASE}/zones/${zoneId}`));
export const setPositionCapacity = (positionId: number, capacity_limit: number) =>
  attempt(() => authPut<ApiResponse<boolean>>(`${BASE}/zones/positions/${positionId}/capacity`, { capacity_limit }));
export const getConfig = () => attempt(() => authGet<ApiResponse<Record<string, string>>>(`${BASE}/zones/config`));
export const setConfig = (key: string, value: string) => attempt(() => authPut<ApiResponse<boolean>>(`${BASE}/zones/config/${key}`, { value }));

// ── Items ─────────────────────────────────────────────────────────────────────
export const getItems = (options?: { itemStatus?: string; qualityStatus?: string; zoneId?: number; search?: string; page?: number; pageSize?: number }) =>
  attempt(() => authGet<ApiResponse<Item[]>>(`${BASE}/items`, { ...options }));
export const getItem = (itemId: number) => attempt(() => authGet<ApiResponse<Item>>(`${BASE}/items/${itemId}`));
export const createItem = (input: ItemInput) => attempt(() => authPost<ApiResponse<AllocationResult>>(`${BASE}/items`, input));
export const holdItem = (itemId: number) => attempt(() => authPut<ApiResponse<boolean>>(`${BASE}/items/${itemId}/hold`, {}));
export const releaseItem = (itemId: number) => attempt(() => authPut<ApiResponse<boolean>>(`${BASE}/items/${itemId}/release`, {}));

// ── Work orders and history ───────────────────────────────────────────────────
export const getWorkOrders = (orderType?: string) => attempt(() => authGet<ApiResponse<WorkOrder[]>>(`${BASE}/work-orders`, { orderType }));
export const completeWorkOrder = (id: number) => attempt(() => authPut<ApiResponse<WorkOrder>>(`${BASE}/work-orders/${id}/complete`, {}));
export const flagWorkOrder = (id: number, reason: string) => attempt(() => authPut<ApiResponse<boolean>>(`${BASE}/work-orders/${id}/flag`, { reason }));
export const planDispatch = (item_id: number, dispatch_zone_id?: number) =>
  attempt(() => authPost<ApiResponse<number[]>>(`${BASE}/work-orders/dispatch`, { item_id, dispatch_zone_id }));
export const getActivityFeed = (limit = 50) => attempt(() => authGet<ApiResponse<Movement[]>>(`${BASE}/work-orders/activity-feed`, { limit }));
export const getItemHistory = (itemId: number) => attempt(() => authGet<ApiResponse<Movement[]>>(`${BASE}/work-orders/items/${itemId}/history`));

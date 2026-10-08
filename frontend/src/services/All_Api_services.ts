// The platform's shared service module, reduced to what the frame needs. Same module path and
// function names as the platform, so the copied Header / Sidebar / LoginPage import it unchanged.

import { BASE_URL as API_ROOT, authGet } from '../api';

const BASE_URL = `${API_ROOT}/api`;

/** Platform branding, OEMs and facility types — read by the login page, sidebar and header. */
export function getOemAndFacilityTypes(): Promise<any> {
  return authGet<any>(`${BASE_URL}/Project/GetOemAndFacilityTypes`);
}

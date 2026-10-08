// The platform's shared system types an application may use. Same path, same names.

export interface ProjectOption {
  id: number;
  name: string;
  customer_logo?: string;
  oem_logo?: string;
  customer_name?: string;
  oem_name?: string;
  application_name?: string;
}

export interface SubprojectOption {
  id: number;
  name: string;
  projectId?: number;
  projectName?: string;
  oem_logo?: string;
  application_name?: string;
}

/** One child entry of an expandable application group in the sidebar. */
export interface AppNavChild {
  label: string;
  route_path: string;
  icon: string;
  permission_code: string;
}

// The platform's ProjectContext — same interface, same sessionStorage keys — fed from the dev
// host's project list instead of the platform's routing tables. The copied Header reads it
// unchanged; application pages may use useProjectContext() exactly as they will in the platform.

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { BASE_URL, authGet } from '../api';
import type { ApiResponse } from '../types';
import type { ProjectOption, SubprojectOption } from '../types/system';

interface ProjectContextType {
  projectId: string;
  projectName: string;
  subProjectId: string;
  subProjectName: string;
  applicationName: string;
  projects: ProjectOption[];
  subProjects: SubprojectOption[];
  subProjectsLoading: boolean;
  setProject: (id: string, name: string, appName?: string) => void;
  setSubProject: (id: string, name: string, appName?: string) => void;
  schemaName: string | undefined;
  isPlatform: boolean;
}

interface DevProject {
  project_id: number;
  project_name: string;
  schema: string;
  customer_name?: string;
  oem_name?: string;
  application_name?: string;
  sub_projects: Array<{
    sub_project_id: number;
    sub_project_name: string;
    schema: string;
    facility_type_id?: number | null;
    facility_type_name?: string | null;
  }>;
}

const ProjectContext = createContext<ProjectContextType | undefined>(undefined);

export function ProjectProvider({ children }: { children: ReactNode }) {
  const [projectId, setProjectId] = useState<string>(() => sessionStorage.getItem('pw_projectId') || '');
  const [projectName, setProjectName] = useState<string>(() => sessionStorage.getItem('pw_projectName') || '');
  const [subProjectId, setSubProjectId] = useState<string>(() => sessionStorage.getItem('pw_subProjectId') || '');
  const [subProjectName, setSubProjectName] = useState<string>(() => sessionStorage.getItem('pw_subProjectName') || '');
  const [applicationName, setApplicationName] = useState<string>(() => sessionStorage.getItem('pw_applicationName') || '');
  const [raw, setRaw] = useState<DevProject[]>([]);

  useEffect(() => {
    authGet<ApiResponse<DevProject[]>>(`${BASE_URL}/api/dev/projects`)
      .then(res => setRaw(res.data ?? []))
      .catch(() => setRaw([]));
  }, []);

  const projects = useMemo<ProjectOption[]>(
    () => raw.map(p => ({
      id: p.project_id,
      name: p.project_name,
      customer_name: p.customer_name,
      oem_name: p.oem_name,
      application_name: p.application_name,
    })),
    [raw],
  );

  const subProjects = useMemo<SubprojectOption[]>(() => {
    const p = raw.find(x => String(x.project_id) === projectId);
    return (p?.sub_projects ?? []).map(s => ({
      id: s.sub_project_id,
      name: s.sub_project_name,
      projectId: p?.project_id,
      projectName: p?.project_name,
      application_name: p?.application_name,
      facility_type_id: s.facility_type_id,
      facility_type_name: s.facility_type_name,
    }));
  }, [raw, projectId]);

  // Once the list arrives: default to the first project so pages have a tenant immediately (the
  // platform does the same for a single-project user), and backfill names for a selection that
  // survived in sessionStorage without them.
  useEffect(() => {
    if (raw.length === 0) return;
    if (!projectId) {
      const first = raw[0];
      setProject(String(first.project_id), first.project_name, first.application_name);
      return;
    }
    const current = raw.find(p => String(p.project_id) === projectId);
    if (!current) return;
    if (!projectName) {
      setProjectName(current.project_name);
      sessionStorage.setItem('pw_projectName', current.project_name);
    }
    if (subProjectId && !subProjectName) {
      const sp = current.sub_projects.find(s => String(s.sub_project_id) === subProjectId);
      if (sp) {
        setSubProjectName(sp.sub_project_name);
        sessionStorage.setItem('pw_subProjectName', sp.sub_project_name);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [raw]);

  const setProject = (id: string, name: string, appName?: string) => {
    if (id !== projectId) {
      setProjectId(id);
      setProjectName(name);
      sessionStorage.setItem('pw_projectId', id);
      sessionStorage.setItem('pw_projectName', name);
      const finalAppName = appName || '';
      setApplicationName(finalAppName);
      sessionStorage.setItem('pw_applicationName', finalAppName);
      setSubProject('', '');
    } else if (appName !== undefined && appName !== '') {
      setApplicationName(appName);
      sessionStorage.setItem('pw_applicationName', appName);
    }
  };

  const setSubProject = (id: string, name: string, appName?: string) => {
    setSubProjectId(id);
    setSubProjectName(name);
    if (id) {
      sessionStorage.setItem('pw_subProjectId', id);
      sessionStorage.setItem('pw_subProjectName', name);
      if (appName?.trim()) {
        setApplicationName(appName.trim());
        sessionStorage.setItem('pw_applicationName', appName.trim());
      }
    } else {
      sessionStorage.removeItem('pw_subProjectId');
      sessionStorage.removeItem('pw_subProjectName');
    }
  };

  const schemaName = useMemo(() => {
    const p = raw.find(x => String(x.project_id) === projectId);
    if (!p) return undefined;
    if (subProjectId) return p.sub_projects.find(s => String(s.sub_project_id) === subProjectId)?.schema ?? p.schema;
    return p.schema;
  }, [raw, projectId, subProjectId]);

  const value: ProjectContextType = {
    projectId, projectName, subProjectId, subProjectName, applicationName,
    projects, subProjects, subProjectsLoading: false,
    setProject, setSubProject, schemaName,
    isPlatform: projectName === 'Platform',
  };

  return <ProjectContext.Provider value={value}>{children}</ProjectContext.Provider>;
}

export function useProjectContext(): ProjectContextType {
  const ctx = useContext(ProjectContext);
  if (!ctx) throw new Error('useProjectContext must be used within ProjectProvider');
  return ctx;
}

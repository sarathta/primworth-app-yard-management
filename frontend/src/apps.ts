// The application registry of the dev shell: reads app.manifest.json and resolves the page
// components it names, so routes and menu are driven by the manifest — the same manifest
// integrate.ps1 uses to register the app in the platform.

import { lazy, type ComponentType, type LazyExoticComponent } from 'react';
import manifest from '../../app.manifest.json';

export interface AppNavItem {
  label: string;
  route_path: string;
  icon: string;
  permission_code: string;
}

export interface AppRoute {
  /** '' for the index route, otherwise a segment under routeBase, e.g. 'grades'. */
  path: string;
  /** Component file name under src/pages/<folder>/, without extension. */
  component: string;
}

export interface AppManifest {
  key: string;
  name: string;
  scope: string;
  icon: string;
  permissionCode: string;
  frontend: {
    folder: string;
    routeBase: string;
    root: string;
    routes: AppRoute[];
    nav: AppNavItem[];
  };
}

const pages = import.meta.glob('./pages/**/*.tsx') as Record<string, () => Promise<{ default: ComponentType }>>;

export function pageComponent(folder: string, name: string): LazyExoticComponent<ComponentType> {
  const key = `./pages/${folder}/${name}.tsx`;
  const loader = pages[key];
  if (!loader) throw new Error(`app.manifest.json names a page that does not exist: src/pages/${folder}/${name}.tsx`);
  return lazy(loader);
}

export const apps: AppManifest[] = [manifest as AppManifest];

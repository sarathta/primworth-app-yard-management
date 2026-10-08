import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import Sidebar from './Sidebar';
import Header from './Header';
import ActivityLoader from './common/ActivityLoader';
import { ProjectProvider } from '../contexts/ProjectContext';

// The platform's Shell: sidebar on the left, header on top, the current page in the scrollable
// content area. Identical markup and classes; the only omission is the license gate, which
// needs the license service the platform runs and the dev host does not.
export default function Shell(): React.JSX.Element {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  return (
    <ProjectProvider>
      <div className={`app-shell${sidebarCollapsed ? ' sidebar-collapsed' : ''}`}>
        <Sidebar collapsed={sidebarCollapsed} onCollapsedChange={setSidebarCollapsed} />
        <div className="app-shell__main">
          <Header />
          <main className="app-content">
            <Outlet />
          </main>
        </div>
      </div>
      <ActivityLoader />
    </ProjectProvider>
  );
}

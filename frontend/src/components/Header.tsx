import { useState, useRef, useEffect } from 'react';
import { getUserData, logout } from '../auth';
import { useProjectContext } from '../contexts/ProjectContext';
import { BASE_URL } from '../api';

export default function Header(): React.JSX.Element {
  const userData = getUserData();
  const fullName = [userData?.first_name, userData?.last_name].filter(Boolean).join(' ') || userData?.email || 'User';
  const roleName = userData?.role_name || (userData?.is_super_admin ? 'Super Admin' : 'User');
  const initials = fullName
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((p: string) => p[0]?.toUpperCase())
    .join('');

  const [open, setOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  
  const { 
    projectId, projectName, subProjectId, subProjectName, 
    projects, subProjects, subProjectsLoading, 
    setProject, setSubProject 
  } = useProjectContext();

  // Close dropdown when clicking outside
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    if (open) document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  const selectedProj = projects.find(p => String(p.id) === String(projectId));
  const selectedSubProj = subProjects.find(sp => String(sp.id) === String(subProjectId));

  const customerLogo = selectedProj?.customer_logo;
  const oemLogo = selectedProj?.oem_logo;

  return (
    <header className="app-header">
      {/* Left side: Customer block */}
      <div className="app-header__left">
        {selectedProj && selectedProj.customer_name && (
          <div className="header-context-block">
            {customerLogo ? (
              <img
                src={customerLogo.startsWith('data:') || customerLogo.startsWith('blob:') || customerLogo.startsWith('http') ? customerLogo : `${BASE_URL}${customerLogo}`}
                alt="Customer Logo"
                className="header-context-block__logo"
              />
            ) : (
              <span className="material-symbols-outlined header-context-block__icon header-context-block__icon--grey">corporate_fare</span>
            )}
            <div className="header-context-block__info">
              <span className="header-context-block__label">Customer</span>
              <span className="header-context-block__value">{selectedProj.customer_name || 'Customer'}</span>
            </div>
          </div>
        )}
      </div>

      {/* Right side: Project, Sub‑Project, OEM */}
      <div className="app-header__right">
        {/* Project Section */}
        <div className="header-context-block">
          <span className="material-symbols-outlined header-context-block__icon header-context-block__icon--blue">folder</span>
          <div className="header-context-block__info">
            <span className="header-context-block__label">Project</span>
            <span className="header-context-block__value">{projectName || 'Platform'}</span>
          </div>
        </div>

        {/* Sub Project Section */}
        {subProjectName && (
          <div className="header-context-block">
            <span className="material-symbols-outlined header-context-block__icon header-context-block__icon--green">folder_open</span>
            <div className="header-context-block__info">
              <span className="header-context-block__label">Sub Project</span>
              <span className="header-context-block__value">{subProjectName}</span>
            </div>
          </div>
        )}
        {/* OEM Block */}
        {selectedProj && selectedProj.oem_name && (
          <div className="header-context-block header-context-block--last">
            {oemLogo ? (
              <img
                src={oemLogo.startsWith('data:') || oemLogo.startsWith('blob:') || oemLogo.startsWith('http')
                  ? oemLogo
                  : `${BASE_URL}${oemLogo}`}
                alt="OEM Logo"
                className="header-context-block__logo"
              />
            ) : (
              <span className="material-symbols-outlined header-context-block__icon header-context-block__icon--grey">
                precision_manufacturing
              </span>
            )}
            <div className="header-context-block__info">
              <span className="header-context-block__label">OEM</span>
              <span className="header-context-block__value">{selectedProj.oem_name}</span>
            </div>
          </div>
        )}
      </div>
    </header>
  );
}

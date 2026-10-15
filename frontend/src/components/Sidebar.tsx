import { Fragment, useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { getUserData, hasPermission, logout } from '../auth';
import { getOemAndFacilityTypes } from '../services/All_Api_services';
import { apps, type AppNavItem } from '../apps';
import { useYardSelection, type SelectedYard } from '../contexts/YardSelectionContext';

interface SidebarProps {
  collapsed: boolean;
  onCollapsedChange: (collapsed: boolean) => void;
}

/**
 * The platform's sidebar, with one difference: its application groups come from
 * app.manifest.json instead of the hand-written list the platform still carries. Markup and
 * class names are the platform's, so what the developer sees here is what the platform shows
 * once integrate.ps1 has added the same entry there.
 */
export default function Sidebar({ collapsed, onCollapsedChange }: SidebarProps): React.JSX.Element {
  const location = useLocation();
  const [openKeys, setOpenKeys] = useState<Record<string, boolean>>({});
  const { selected: selectedYards } = useYardSelection();
  const yardTypeId = new URLSearchParams(location.search).get('yardTypeId');

  // Profile dropdown state and logic
  const dropdownRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const userData = getUserData();
  const fullName = [userData?.first_name, userData?.last_name].filter(Boolean).join(' ') || userData?.email || 'User';
  const roleName = userData?.role_name || (userData?.is_super_admin ? 'Super Admin' : 'User');
  const initials = fullName.split(' ').filter(Boolean).slice(0, 2).map((p: string) => p[0]?.toUpperCase()).join('');

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) setOpen(false);
    }
    if (open) document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  // Platform branding
  const [platform, setPlatform] = useState<{ name: string; logo?: string; powered_by?: string } | null>(null);
  useEffect(() => {
    getOemAndFacilityTypes()
      .then((res) => {
        if (res?.success && res?.data?.platforms?.length > 0) {
          const first = res.data.platforms[0];
          setPlatform({ name: first.name || '-', logo: first.logo || undefined, powered_by: first.powered_by || undefined });
        }
      })
      .catch((err) => { console.error('Failed to load platform settings', err); });
  }, []);

  const isOpen = (key: string, onRoute: boolean) => openKeys[key] ?? onRoute;

  const yardOpen = (key: string) => openKeys[key] ?? true;

  const childActive = (item: AppNavItem, yard: SelectedYard) => {
    const path = location.pathname.replace(/\/+$/, '') || '/';
    const route = item.route_path.replace(/\/+$/, '') || '/';
    const onPath = route === '/yard' ? path === '/yard' : path === route || path.startsWith(`${route}/`);
    return onPath && yardTypeId === String(yard.yard_type_id);
  };

  return (
    <aside className={`sidebar${collapsed ? ' sidebar--collapsed' : ''}`}>
      <div className="sidebar__brand">
        {platform?.logo ? (
          <div className="sidebar__brand-logo-container">
            <img src={platform.logo} alt={platform.name} className="sidebar__brand-logo" />
            <div className="sidebar__brand-info">
              <h1 className="sidebar__brand-title">{platform.name}</h1>
              <p className="sidebar__brand-subtitle">{platform.powered_by || '-'}</p>
            </div>
          </div>
        ) : (
          <>
            <h1>{platform?.name || '-'}</h1>
            <p> {platform?.powered_by || '-'}</p>
          </>
        )}
      </div>

      <nav className="sidebar__nav">
        {apps.filter(app => hasPermission(app.permissionCode)).map(app => {
          if (app.key === 'yard_management' && selectedYards.length > 0) {
            const items = app.frontend.nav.filter(item => hasPermission(item.permission_code));
            return selectedYards.map(yard => {
              const key = `yard-${yard.yard_type_id}`;
              const expanded = yardOpen(key);
              return (
                <div className="sidebar__yard" key={key}>
                  <button
                    type="button"
                    className="sidebar__nav-item sidebar__nav-group sidebar__yard-head"
                    onClick={() => setOpenKeys(k => ({ ...k, [key]: !expanded }))}
                    aria-label={`Toggle ${yard.yard_type_name} menu`}
                    aria-expanded={expanded}
                  >
                    <span className="material-symbols-outlined">my_location</span>
                    <span className="sidebar__nav-label">{yard.yard_type_name}</span>
                    <span className="material-symbols-outlined sidebar__nav-chevron">
                      {expanded ? 'expand_less' : 'expand_more'}
                    </span>
                  </button>
                  {expanded && (
                    <div className="sidebar__subnav">
                      {items.map(item => {
                        const active = childActive(item, yard);
                        return (
                          <Link
                            key={`${yard.yard_type_id}-${item.route_path}`}
                            to={`${item.route_path}?yardTypeId=${yard.yard_type_id}`}
                            aria-current={active ? 'page' : undefined}
                            className={'sidebar__nav-item sidebar__nav-item--child' + (active ? ' sidebar__nav-item--active' : '')}
                          >
                            <span className="material-symbols-outlined">{item.icon}</span>
                            {item.label}
                          </Link>
                        );
                      })}
                    </div>
                  )}
                </div>
              );
            });
          }

          const onRoute = location.pathname.startsWith(app.frontend.routeBase);
          const expanded = isOpen(app.key, onRoute);
          const waitingForYards = app.key === 'yard_management' && selectedYards.length === 0;
          return (
            <Fragment key={app.key}>
              {waitingForYards ? (
                <NavLink
                  to="/yard"
                  className={({ isActive }) =>
                    'sidebar__nav-item sidebar__nav-group' + (isActive ? ' sidebar__nav-item--active' : '')
                  }
                >
                  <span className="material-symbols-outlined">{app.icon}</span>
                  <span className="sidebar__nav-label">{app.name}</span>
                </NavLink>
              ) : (
                <button
                  type="button"
                  className={'sidebar__nav-item sidebar__nav-group' + (onRoute ? ' sidebar__nav-item--active' : '')}
                  onClick={() => setOpenKeys(k => ({ ...k, [app.key]: !expanded }))}
                  aria-label={`Toggle ${app.name} menu`}
                >
                  <span className="material-symbols-outlined">{app.icon}</span>
                  <span className="sidebar__nav-label">{app.name}</span>
                  <span className="material-symbols-outlined sidebar__nav-chevron">
                    {expanded ? 'expand_less' : 'expand_more'}
                  </span>
                </button>
              )}

              {!waitingForYards && expanded && (
                <div className="sidebar__subnav">
                  {app.frontend.nav.filter(item => hasPermission(item.permission_code)).map(item => (
                    <NavLink
                      key={item.route_path}
                      to={item.route_path}
                      end={item.route_path === app.frontend.routeBase}
                      className={({ isActive }) =>
                        'sidebar__nav-item sidebar__nav-item--child' + (isActive ? ' sidebar__nav-item--active' : '')
                      }
                    >
                      <span className="material-symbols-outlined">{item.icon}</span>
                      {item.label}
                    </NavLink>
                  ))}
                </div>
              )}
            </Fragment>
          );
        })}
      </nav>

      <div className="sidebar__footer">
        <button
          type="button"
          className="sidebar__collapse-toggle"
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          onClick={() => onCollapsedChange(!collapsed)}
        >
          <span className="material-symbols-outlined">{collapsed ? 'chevron_right' : 'chevron_left'}</span>
          <span className="sidebar__collapse-toggle-label">{collapsed ? 'Expand' : 'Collapse'}</span>
        </button>
        <div className="header-profile" ref={dropdownRef}>
          <button
            className="header-profile__trigger"
            onClick={() => setOpen(v => !v)}
            aria-haspopup="true"
            aria-expanded={open}
          >
            <div className="header-profile__avatar">{initials}</div>
            <div className="header-profile__info">
              <span className="header-profile__name">{fullName}</span>
              <span className="header-profile__role">{roleName}</span>
            </div>
            <span className={`material-symbols-outlined header-profile__chevron${open ? ' header-profile__chevron--open' : ''}`}>expand_more</span>
          </button>

          {open && (
            <div className="header-profile__dropdown">
              <div className="header-profile__dropdown-header">
                <div className="header-profile__avatar header-profile__avatar--lg">{initials}</div>
                <div>
                  <div className="header-profile__dropdown-name">{fullName}</div>
                  <div className="header-profile__dropdown-role">{roleName}</div>
                </div>
              </div>
              <div className="header-profile__dropdown-divider" />
              <div className="header-profile__dropdown-email">
                <span className="material-symbols-outlined">mail</span>
                {userData?.email || '—'}
              </div>
              <div className="header-profile__dropdown-divider" />
              <button className="header-profile__dropdown-logout" onClick={logout}>
                <span className="material-symbols-outlined">logout</span>
                Logout
              </button>
            </div>
          )}
        </div>
      </div>
    </aside>
  );
}

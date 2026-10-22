import { Outlet, Navigate } from 'react-router-dom';
import { useYardSelection } from '../../../../contexts/YardSelectionContext';
import { useYardMaterial } from '../../components/MaterialGate';
import { useAuth } from '../contexts/AuthContext';
import { useSocket } from '../contexts/SocketContext';

/**
 * Frame for the mSYMS screens inside the platform.
 *
 * Standalone, this rendered mSYMS's own 256px sidebar — logo, six navigation links, and a
 * user block with sign-out. Inside the platform every one of those is a duplicate: the
 * platform's sidebar carries the same six screens under Slab Yard, its header carries the
 * customer, project and sub project, and its footer carries the signed-in user. Two
 * sidebars stacked side by side is how an embedded app looks when it has been dropped in
 * rather than absorbed.
 *
 * What survives is the one thing that is genuinely mSYMS's and has nowhere else to live:
 * whether the yard's live link is up. An operator needs to know at a glance whether what
 * they are looking at is current, so it stays — as a strip above the screen rather than a
 * corner of a sidebar that no longer exists.
 *
 * The screens themselves are untouched and still render through <Outlet />.
 */
export const Layout = () => {
  const { user } = useAuth();
  const { connected, lastContactAt } = useSocket();
  const { assigned } = useYardSelection();
  const { material, changeMaterial } = useYardMaterial();

  // AuthContext projects the platform session into the shape these screens expect, so a
  // missing user here means the platform session itself has gone — let the platform's own
  // guard handle it rather than deciding anything about routing here.
  if (!user) {
    return <Navigate to="/login" replace />;
  }

  return (
    <div className="msyms-frame">
      <div className="msyms-linkbar" role="status" aria-live="polite">
        <span className={`msyms-linkbar__dot msyms-linkbar__dot--${connected ? 'on' : 'off'}`} />
        {connected
          ? 'Yard link online'
          : lastContactAt
            ? `Yard link offline — last answer ${lastContactAt.toLocaleTimeString()}, figures may be stale`
            : 'Yard link offline — figures may be stale'}
        <span className="msyms-linkbar__material">{material.singular}</span>
        {assigned.length > 1 && (
          <button type="button" className="msyms-linkbar__change" onClick={changeMaterial}>
            Change yards
          </button>
        )}
      </div>

      <div className="msyms-screen">
        <Outlet />
      </div>
    </div>
  );
};

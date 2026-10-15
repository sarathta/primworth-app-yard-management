import { useEffect } from 'react';
import { Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useProjectContext } from '../../../contexts/ProjectContext';
import { useYardSelection } from '../../../contexts/YardSelectionContext';
import { YardMaterialScope } from '../components/MaterialGate';
import SelectYardsNotice from '../components/SelectYardsNotice';
import { materialFromYard } from '../yardMaterial';
import { AuthProvider } from './contexts/AuthContext';
import { SocketProvider } from './contexts/SocketContext';
import { Layout } from './components/Layout';
import MsymsErrorBoundary from './MsymsErrorBoundary';
import { setSymsFacilityType, setSymsMaterial, setSymsTenant, setSymsYardType } from './symsApi';
import './msyms.css';
import './slab-yard.css';

/**
 * Mount point for the embedded mSYMS UI.
 *
 * After login the operator picks yards from sp_yard_type. Each chosen yard is a menu
 * group on the left; the open group is yardTypeId on the address, and every screen
 * request is limited to the areas of that yard.
 */
export default function MsymsRoot(): React.JSX.Element {
  const { projectId, subProjectId, subProjects, projects, setSubProject, isPlatform } = useProjectContext();
  const { selected } = useYardSelection();
  const [params] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    if (subProjectId || subProjects.length === 0) return;
    const first = subProjects[0];
    setSubProject(String(first.id), first.name, 'Yard Management');
  }, [subProjectId, subProjects, setSubProject]);

  setSymsTenant({ projectId, subProjectId });

  const choosing = params.get('choose') === '1' || selected.length === 0;
  const yardId = Number(params.get('yardTypeId'));
  const yard = !choosing ? selected.find(y => y.yard_type_id === yardId) : undefined;

  setSymsYardType(yard?.yard_type_id ?? null);
  setSymsMaterial('');
  setSymsFacilityType(null);

  if (isPlatform || !projectId) {
    return (
      <div className="slab-yard">
        <div className="system-empty-state">
          <span className="material-symbols-outlined">warehouse</span>
          <h3>Select yards</h3>
          <p>Sign in to a project to open its yards.</p>
        </div>
      </div>
    );
  }

  if (!subProjectId && (projects.length === 0 || subProjects.length > 0)) {
    return (
      <div className="slab-yard">
        <div className="system-empty-state"><p>Loading yards...</p></div>
      </div>
    );
  }

  if (choosing) {
    if (location.pathname !== '/yard') return <Navigate to="/yard?choose=1" replace />;
    return (
      <div className="slab-yard">
        <SelectYardsNotice />
      </div>
    );
  }

  if (!yard) {
    const path = location.pathname.startsWith('/yard') ? location.pathname : '/yard';
    return <Navigate to={`${path}?yardTypeId=${selected[0].yard_type_id}`} replace />;
  }

  return (
    <YardMaterialScope
      material={materialFromYard(yard.yard_type_name)}
      onChangeYards={() => navigate('/yard?choose=1')}
    >
      <div className="msyms-root" key={yard.yard_type_id}>
        <MsymsErrorBoundary key={`${projectId}:${subProjectId}:${yard.yard_type_id}`}>
          <AuthProvider>
            <SocketProvider>
              <Layout />
            </SocketProvider>
          </AuthProvider>
        </MsymsErrorBoundary>
      </div>
    </YardMaterialScope>
  );
}

import { useProjectContext } from '../../../contexts/ProjectContext';
import SelectSubProjectNotice from '../components/SelectSubProjectNotice';
import { AuthProvider } from './contexts/AuthContext';
import { SocketProvider } from './contexts/SocketContext';
import { Layout } from './components/Layout';
import MsymsErrorBoundary from './MsymsErrorBoundary';
import { setSymsTenant } from './symsApi';
import './msyms.css';
// Only for the "select a sub project" notice, which renders outside .msyms-root and so
// uses the platform's own styling rather than Tailwind.
import './slab-yard.css';

/**
 * Mount point for the embedded mSYMS UI.
 *
 * Replaces the original App.tsx, which owned a BrowserRouter and a Login route. The
 * platform already provides both, so this keeps only what is genuinely mSYMS's: its two
 * context providers and its Layout (the dark sidebar with Dashboard / Active Work /
 * Dispatch Engine / QA Inspector / Mill Operations / Configurator). The screens themselves
 * are untouched and render through Layout's <Outlet />, as child routes declared in
 * App.tsx.
 *
 * `.msyms-root` is what confines Tailwind: every generated utility is compiled under that
 * selector, so the mSYMS styles cannot reach the platform shell around it.
 */
export default function MsymsRoot(): React.JSX.Element {
  const { projectId, subProjectId, isPlatform } = useProjectContext();

  // The screens call the API from plain functions, not components, so they cannot read
  // React context. The tenant is pushed into the fetch shim instead.
  //
  // Set during render, NOT in an effect: React runs child effects before parent effects,
  // so a useEffect here would fire after Dashboard's first fetch — which would then go out
  // with no tenant, resolve no schema, and come back empty. Writing a module variable
  // during render is safe because it is idempotent and touches no React state.
  setSymsTenant({ projectId, subProjectId });

  const scoped = !isPlatform && !!projectId && !!subProjectId;

  // Standalone mSYMS was single-tenant and had no notion of this; inside the platform a
  // yard belongs to a facility, and without one there is no database schema to read.
  if (!scoped) {
    return <div className="slab-yard"><SelectSubProjectNotice action="open its yard" /></div>;
  }

  // Keyed on the facility, which remounts every screen below when the user switches
  // sub project.
  //
  // Without it the tenant changed under screens that never re-read it: their fetch effects
  // depend on [token] or [socket], not on the facility, so Dashboard and QA showed the
  // previous yard's slabs until the 15-second poll happened to fire, and Mill Schedule and
  // the Configurator — which register no poll at all — showed them indefinitely. Worse, a
  // write issued in that window carried the new facility in the query string while the row
  // id came from the old one.
  //
  // Remounting is the honest fix here rather than threading the facility through 53 call
  // sites: switching plant is a change of everything on screen, not a refresh of it.
  return (
    <div className="msyms-root">
      <MsymsErrorBoundary key={`${projectId}:${subProjectId}`}>
        <AuthProvider>
          <SocketProvider>
            <Layout />
          </SocketProvider>
        </AuthProvider>
      </MsymsErrorBoundary>
    </div>
  );
}

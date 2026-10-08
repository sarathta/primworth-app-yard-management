import { Link } from 'react-router-dom';
import { useProjectContext } from '../../../contexts/ProjectContext';

/**
 * Shown when the yard is opened without a sub project selected.
 *
 * A yard belongs to a facility, and the platform keeps the selection on the Project page, on a
 * checkbox inside the expanded project row, which is not somewhere you would guess. So this
 * spells out the steps and links straight there, and, because the project context already
 * knows the facilities, offers them here as well: one click, no detour. In the starter kit's
 * dev shell there is no Project page, and the buttons are the only way to pick one.
 */
export default function SelectSubProjectNotice({ action = 'open' }: { action?: string }): React.JSX.Element {
  const { subProjects, subProjectsLoading, setSubProject } = useProjectContext();

  return (
    <div className="system-empty-state">
      <span className="material-symbols-outlined">view_in_ar</span>
      <h3>Select a facility</h3>
      <p style={{ maxWidth: '520px' }}>
        Yard Management runs per facility, so it needs a sub project to {action}.
      </p>
      {subProjects.length > 0 ? (
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'center', margin: '8px 0' }}>
          {subProjects.map(sp => (
            <button key={sp.id} type="button" className="btn btn--primary"
                    onClick={() => setSubProject(String(sp.id), sp.name, 'Yard Management')}>
              {sp.name}
            </button>
          ))}
        </div>
      ) : (
        <ol style={{ textAlign: 'left', maxWidth: '460px', margin: '0 auto', lineHeight: 1.9, fontSize: '14px' }}>
          <li>Open <Link to="/project">Project</Link>.</li>
          <li>Expand the project with the chevron on its row.</li>
          <li>Tick the checkbox beside the sub project you want.</li>
        </ol>
      )}
      {subProjectsLoading && <p>Loading facilities...</p>}
    </div>
  );
}

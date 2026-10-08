import { Suspense, type ReactNode } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { apps, pageComponent } from './apps';
import { homeRouteForUser, isLoggedIn } from './auth';
import LoginPage from './components/auth/LoginPage';
import Shell from './components/Shell';

// Resolve every page once, at module load — lazy() must not be called during render.
const mounted = apps.map(app => ({
  app,
  Root: pageComponent(app.frontend.folder, app.frontend.root),
  pages: app.frontend.routes.map(route => ({ route, Page: pageComponent(app.frontend.folder, route.component) })),
}));

function ProtectedRoute({ children }: { children: ReactNode }) {
  return isLoggedIn() ? <>{children}</> : <Navigate to="/login" replace />;
}

// The platform's routing shape: /login outside the shell, everything else inside it. The
// application's routes are the block integrate.ps1 adds to the platform's App.tsx.
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<ProtectedRoute><Shell /></ProtectedRoute>}>
        <Route index element={<Navigate to={homeRouteForUser()} replace />} />
        {mounted.map(({ app, Root, pages }) => (
          <Route
            key={app.key}
            path={app.frontend.routeBase.replace(/^\//, '')}
            element={<Suspense fallback={null}><Root /></Suspense>}
          >
            {pages.map(({ route, Page }) =>
              route.path === '' ? (
                <Route key="index" index element={<Suspense fallback={null}><Page /></Suspense>} />
              ) : (
                <Route key={route.path} path={route.path} element={<Suspense fallback={null}><Page /></Suspense>} />
              ),
            )}
          </Route>
        ))}
        <Route path="*" element={<div className="page-header"><div><h2>Not found</h2><p className="page-sub">There is no page at this address.</p></div></div>} />
      </Route>
    </Routes>
  );
}

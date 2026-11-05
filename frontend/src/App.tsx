import { Link, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { useEffect, type ReactNode } from 'react';
import { useAuth } from './auth/AuthContext';
import { AdminPage } from './pages/AdminPage';
import { DashboardPage } from './pages/DashboardPage';
import { IncidentDetailPage } from './pages/IncidentDetailPage';
import { LoginPage } from './pages/LoginPage';
import { LandingPage } from './pages/LandingPage';
import { NewIncidentPage } from './pages/NewIncidentPage';
import { IS_DEMO } from './demo/config';
import { readableLabel } from './ui';

function Shell({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  return (
    <div className="app-shell">
      <a className="skip-link" href="#workspace">Skip to workspace</a>
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true" />
          <div>
            <strong>PulseLink</strong>
            <small>Care continuity</small>
          </div>
        </div>
        {user && (
          <div className="topbar-right">
            <nav aria-label="Workspace">
              <Link to="/">Incidents</Link>
              {user.role === 'Admin' && <Link to="/admin">Admin</Link>}
            </nav>
            <div className="user-chip">
              <span className="user-identity">
                <span className="user-name">{user.displayName}</span>
                <span className="user-role">{readableLabel(user.role)}</span>
              </span>
              <button type="button" className="ghost" onClick={() => {
                logout();
                if (IS_DEMO) navigate('/login', { replace: true });
              }}>
                {IS_DEMO ? 'Switch role' : 'Sign out'}
              </button>
            </div>
          </div>
        )}
      </header>
      <main id="workspace" tabIndex={-1}>{children}</main>
    </div>
  );
}

function Protected({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  return <Shell>{children}</Shell>;
}

export default function App() {
  const { user } = useAuth();
  const { pathname } = useLocation();
  useEffect(() => {
    const main = document.querySelector('main');
    if (main) { main.tabIndex = -1; main.focus({ preventScroll: true }); }
    window.scrollTo(0, 0);
  }, [pathname, user?.role]);
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          user ? <Protected><DashboardPage /></Protected> : <LandingPage />
        }
      />
      <Route
        path="/incidents/new"
        element={
          <Protected>
            <NewIncidentPage />
          </Protected>
        }
      />
      <Route
        path="/incidents/:id"
        element={
          <Protected>
            <IncidentDetailPage />
          </Protected>
        }
      />
      <Route
        path="/admin"
        element={
          <Protected>
            <AdminPage />
          </Protected>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

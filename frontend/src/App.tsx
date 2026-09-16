import { Link, Navigate, Route, Routes } from 'react-router-dom';
import type { ReactNode } from 'react';
import { useAuth } from './auth/AuthContext';
import { AdminPage } from './pages/AdminPage';
import { DashboardPage } from './pages/DashboardPage';
import { IncidentDetailPage } from './pages/IncidentDetailPage';
import { LoginPage } from './pages/LoginPage';
import { LandingPage } from './pages/LandingPage';
import { NewIncidentPage } from './pages/NewIncidentPage';

function Shell({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark" />
          <div>
            <strong>PulseLink</strong>
            <small>Care continuity</small>
          </div>
        </div>
        {user && (
          <div className="topbar-right">
            <nav>
              <Link to="/">Incidents</Link>
              {user.role === 'Admin' && <Link to="/admin">Admin</Link>}
            </nav>
            <div className="user-chip">
              <span>
                {user.displayName} · {user.role}
              </span>
              <button type="button" className="ghost" onClick={logout}>
                Sign out
              </button>
            </div>
          </div>
        )}
      </header>
      <main>{children}</main>
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

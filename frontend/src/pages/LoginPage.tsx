import { useState, type FormEvent } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { IS_DEMO } from '../demo/config';

const demos = [
  { role: 'Paramedic', email: 'paramedic@pulselink.demo', description: 'Document observations and move a patient through transport.' },
  { role: 'Hospital', email: 'hospital@pulselink.demo', description: 'Review incoming reports and acknowledge hospital receipt.' },
  { role: 'Admin', email: 'admin@pulselink.demo', description: 'Explore incidents across agencies and hospital directories.' },
];

export function LoginPage() {
  const { user, login } = useAuth();
  const [email, setEmail] = useState(import.meta.env.DEV ? 'paramedic@pulselink.demo' : '');
  const [password, setPassword] = useState(import.meta.env.DEV ? 'Demo123!' : '');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  if (user) return <Navigate to="/" replace />;

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login(email, password);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="login-shell">
      <div className="login-panel">
        <Link className="login-back" to="/">← PulseLink home</Link>
        <p className="eyebrow">YOUR CARE WORKSPACE</p>
        <h1>{IS_DEMO ? 'Choose your perspective.' : 'Welcome back.'}</h1>
        <p className="lede">
          {IS_DEMO ? 'Explore the same handoff from the field and the receiving hospital. No account or password needed.' : 'Sign in to create field PCRs, advance transport status, and deliver structured hospital handoffs.'}
        </p>
        {!IS_DEMO && <form onSubmit={onSubmit} className="stack">
          <label>
            Email
            <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" autoComplete="username" required />
          </label>
          <label>
            Password
            <input
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              type="password"
              autoComplete="current-password"
              required
            />
          </label>
          <button type="submit" disabled={loading}>
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>}
        {error && <p className="error" role="alert">{error}</p>}
        {IS_DEMO && <div className="demo-accounts">
          <div className="role-options">
            {demos.map((d) => (
              <button
                key={d.email}
                type="button"
                className="role-option ghost"
                aria-label={`Continue as ${d.role}`}
                disabled={loading}
                onClick={() => {
                  setEmail(d.email);
                  setError('');
                  setLoading(true);
                  login(d.email, 'demo').catch((err: unknown) => {
                    setError(err instanceof Error ? err.message : 'Sign in failed');
                  }).finally(() => setLoading(false));
                }}
              >
                <strong>Continue as {d.role}<span aria-hidden="true"> →</span></strong>
                <span>{d.description}</span>
              </button>
            ))}
          </div>
          <p role="status">{loading ? 'Opening workspace…' : 'Start as Paramedic, then switch roles to see the receiving team’s view. Demo changes stay in this browser.'}</p>
        </div>}
        {!IS_DEMO && import.meta.env.DEV && <div className="demo-accounts">
          <p>Demo accounts (password: Demo123!)</p>
          <div className="demo-row">
            {demos.map((d) => (
              <button key={d.email} type="button" className="ghost" onClick={() => setEmail(d.email)}>
                {d.role}
              </button>
            ))}
          </div>
        </div>}
      </div>
    </main>
  );
}

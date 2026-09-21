import { useState, type FormEvent } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { IS_DEMO } from '../demo/config';

const demos = [
  { role: 'Paramedic', email: 'paramedic@pulselink.demo' },
  { role: 'Hospital', email: 'hospital@pulselink.demo' },
  { role: 'Admin', email: 'admin@pulselink.demo' },
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
    <div className="login-shell">
      <div className="login-panel">
        <Link className="login-back" to="/">← PulseLink home</Link>
        <p className="eyebrow">YOUR CARE WORKSPACE</p>
        <h1>Welcome back.</h1>
        <p className="lede">
          Sign in to create field PCRs, advance transport status, and deliver structured hospital handoffs.
        </p>
        <form onSubmit={onSubmit} className="stack">
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
          {error && <p className="error">{error}</p>}
          <button type="submit" disabled={loading}>
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
        {IS_DEMO && <div className="demo-accounts">
          <p>Pick a role to continue. No password, and no account is created.</p>
          <div className="demo-row">
            {demos.map((d) => (
              <button
                key={d.email}
                type="button"
                className="ghost"
                disabled={loading}
                onClick={() => {
                  setEmail(d.email);
                  setError('');
                  login(d.email, 'demo').catch((err: unknown) => {
                    setError(err instanceof Error ? err.message : 'Sign in failed');
                  });
                }}
              >
                Continue as {d.role}
              </button>
            ))}
          </div>
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
    </div>
  );
}

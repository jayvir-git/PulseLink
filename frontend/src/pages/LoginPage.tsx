import { useState, type FormEvent } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

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
        <p className="eyebrow">PulseLink</p>
        <h1>EMS to hospital handoff</h1>
        <p className="lede">
          Sign in to create field PCRs, advance transport status, and deliver structured hospital handoffs.
        </p>
        <form onSubmit={onSubmit} className="stack">
          <label>
            Email
            <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" required />
          </label>
          <label>
            Password
            <input
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              type="password"
              required
            />
          </label>
          {error && <p className="error">{error}</p>}
          <button type="submit" disabled={loading}>
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
        {import.meta.env.DEV && <div className="demo-accounts">
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

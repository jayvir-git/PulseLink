import { useEffect, useState } from 'react';
import { api } from '../api/client';

export function AdminPage() {
  const [agencies, setAgencies] = useState<{ id: string; name: string; region: string }[]>([]);
  const [hospitals, setHospitals] = useState<{ id: string; name: string; city: string }[]>([]);
  const [error, setError] = useState('');

  useEffect(() => {
    Promise.all([api.agencies(), api.hospitals()])
      .then(([a, h]) => {
        setAgencies(a);
        setHospitals(h);
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load'));
  }, []);

  return (
    <section className="page">
      <p className="eyebrow">Admin</p>
      <h1>Organizations</h1>
      <p className="lede">Seeded agencies and receiving hospitals in this environment.</p>
      {error && <p className="error">{error}</p>}
      <div className="detail-grid">
        <div className="panel">
          <h2>Agencies</h2>
          <ul className="list">
            {agencies.map((a) => (
              <li key={a.id}>
                <strong>{a.name}</strong>
                <span>{a.region}</span>
              </li>
            ))}
          </ul>
        </div>
        <div className="panel">
          <h2>Hospitals</h2>
          <ul className="list">
            {hospitals.map((h) => (
              <li key={h.id}>
                <strong>{h.name}</strong>
                <span>{h.city}</span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, type IncidentSummary } from '../api/client';
import { useAuth } from '../auth/AuthContext';

export function DashboardPage() {
  const { user } = useAuth();
  const [items, setItems] = useState<IncidentSummary[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .incidents()
      .then(setItems)
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, []);

  const canCreate = user?.role === 'Paramedic' || user?.role === 'Admin';

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="eyebrow">Operations</p>
          <h1>
            {user?.role === 'HospitalStaff' ? 'Incoming handoffs' : 'Incidents'}
          </h1>
          <p className="lede">
            {user?.role === 'HospitalStaff'
              ? 'Transporting, arrived, and handed-off patients destined for your hospital.'
              : 'Field PCR drafts and live transport status for your agency.'}
          </p>
        </div>
        {canCreate && (
          <Link className="button" to="/incidents/new">
            New incident
          </Link>
        )}
      </header>

      {loading && <p>Loading…</p>}
      {error && <p className="error">{error}</p>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Incident</th>
              <th>Status</th>
              <th>Complaint</th>
              <th>Agency</th>
              <th>Destination</th>
              <th>Updated</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>
                  <Link to={`/incidents/${item.id}`}>{item.incidentNumber}</Link>
                </td>
                <td>
                  <span className={`status status-${item.status.toLowerCase()}`}>{item.status}</span>
                </td>
                <td>{item.chiefComplaint}</td>
                <td>{item.agencyName}</td>
                <td>{item.destinationHospitalName ?? '—'}</td>
                <td>{new Date(item.updatedAt).toLocaleString()}</td>
              </tr>
            ))}
            {!loading && items.length === 0 && (
              <tr>
                <td colSpan={6}>No incidents yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}

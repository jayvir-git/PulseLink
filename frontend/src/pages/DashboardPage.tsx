import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, type PagedIncidentList } from '../api/client';
import { useAuth } from '../auth/AuthContext';

export function DashboardPage() {
  const { user } = useAuth();
  const [result, setResult] = useState<PagedIncidentList | null>(null);
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    api
      .incidents(page)
      .then(data => { if (active) setResult(data); })
      .catch((err) => { if (active) setError(err instanceof Error ? err.message : 'Failed to load'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, attempt]);

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
      {error && <button type="button" onClick={() => setAttempt(value => value + 1)}>Retry list</button>}

      <nav className="demo-row" aria-label="Incident pages">
        <button type="button" disabled={page === 1} onClick={() => setPage(value => value - 1)}>Previous</button>
        <span>Page {page}</span>
        <button type="button" disabled={loading || !!error || !result || page * result.pageSize >= result.totalCount}
          onClick={() => setPage(value => value + 1)}>Next</button>
      </nav>
      {!loading && !error && result && (
        <p role="status">{result.items.length > 0
          ? `${(result.page - 1) * result.pageSize + 1}–${(result.page - 1) * result.pageSize + result.items.length} of ${result.totalCount} incidents`
          : `${result.totalCount} incidents`}</p>
      )}

      {!loading && !error && result && (
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
            {result.items.map((item) => (
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
            {result.items.length === 0 && (
              <tr>
                <td colSpan={6}>{page === 1 ? 'No incidents yet.' : 'No incidents on this page. Go to the previous page.'}</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      )}
    </section>
  );
}

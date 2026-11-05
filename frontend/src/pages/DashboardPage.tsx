import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, type PagedIncidentList } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { readableLabel } from '../ui';

export function DashboardPage() {
  const { user } = useAuth();
  const [result, setResult] = useState<PagedIncidentList | null>(null);
  const [page, setPage] = useState(1);
  const [queue, setQueue] = useState('all');
  const [checkedAt, setCheckedAt] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    api
      .incidents(page, queue)
      .then(data => { if (active) { setResult(data); setCheckedAt(new Date().toLocaleTimeString()); } })
      .catch((err) => { if (active) setError(err instanceof Error ? err.message : 'Failed to load'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, attempt, queue]);

  const canCreate = user?.role === 'Paramedic' || user?.role === 'Admin';
  const hospital = user?.role === 'HospitalStaff';
  const queues = [
    { key: 'incoming', label: 'Incoming', detail: 'EMS transporting', count: result?.queueCounts?.incoming },
    { key: 'arrived', label: 'Arrived', detail: 'EMS handoff pending', count: result?.queueCounts?.arrived },
    { key: 'awaiting', label: 'Awaiting acknowledgment', detail: 'Handoff complete · review report', count: result?.queueCounts?.awaiting },
    { key: 'received', label: 'Acknowledged', detail: 'Report receipt confirmed', count: result?.queueCounts?.received },
  ];
  function selectQueue(value: string) { setQueue(value); setPage(1); }
  const receiptLabel = (status: string, acknowledged?: boolean) => status !== 'HandedOff'
    ? 'After EMS handoff' : acknowledged === undefined ? 'Receipt unavailable' : acknowledged ? 'Acknowledged' : 'Awaiting acknowledgment';

  return (
    <section className="page receiving-report queue-page">
      <header className="report-heading">
        <div className="report-kicker"><span>{hospital ? 'HOSPITAL RECEIVING WORKSPACE' : 'FIELD CARE WORKSPACE'}</span>{canCreate && <Link className="button" to="/incidents/new">New incident</Link>}</div>
        <h1>{hospital ? 'Incoming handoffs' : 'Incidents'}</h1>
        <p className="report-caption">{hospital ? 'Patients destined for your hospital, from transport through report receipt.' : 'Care reports and last reported transport status for your agency.'}</p>
      </header>
      {hospital && <nav className="queue-filters" aria-label="Hospital queues">
        {queues.map(item => <button key={item.key} type="button" aria-pressed={queue === item.key} onClick={() => selectQueue(item.key)}>
          <span className="queue-count">{loading || error ? '—' : item.count ?? '—'}</span><strong>{item.label}</strong><small>{item.detail}</small>
        </button>)}
      </nav>}
      <div className="report-toolbar queue-toolbar">
        <div>{hospital && <button className="ghost" type="button" aria-pressed={queue === 'all'} onClick={() => selectQueue('all')}>All reports</button>}<span className="report-caption">{loading ? 'Checking reports…' : error ? 'Refresh failed' : checkedAt ? `Last checked ${checkedAt}` : 'Not yet checked'} · Manual refresh</span></div>
        <button type="button" className="ghost" disabled={loading} onClick={() => setAttempt(value => value + 1)}>Refresh list</button>
      </div>
      <div className="queue-description"><h2>{queue === 'all' ? 'All reports' : queues.find(item => item.key === queue)?.label}</h2><p className="report-caption">{hospital ? 'Status is last reported by EMS. Acknowledgment confirms report delivery, not transfer of clinical responsibility.' : 'Open a report to document care, update transport, or check hospital receipt.'} Newest updated reports first.</p></div>
      {loading && <p role="status">Loading…</p>}
      {error && <p className="error" role="alert">{error}</p>}
      {error && <button type="button" onClick={() => setAttempt(value => value + 1)}>Retry list</button>}


      {!loading && !error && result && (
        <p role="status">{result.items.length > 0
          ? `${(result.page - 1) * result.pageSize + 1}–${(result.page - 1) * result.pageSize + result.items.length} of ${result.totalCount} incidents`
          : `${result.totalCount} incidents`}</p>
      )}

      {!loading && !error && result && (
      <div className="table-wrap incident-table queue-table">
        <table role="table" aria-label={user?.role === 'HospitalStaff' ? 'Incoming handoffs' : 'Incidents'}>
          <thead role="rowgroup">
            <tr role="row">
              <th>Incident</th>
              <th>Status</th>
              <th>Complaint</th>
              <th>Agency</th>
              <th>Report receipt</th>
              {!hospital && <th>Destination</th>}
              <th>Updated</th>
            </tr>
          </thead>
          <tbody role="rowgroup">
            {result.items.map((item) => (
              <tr key={item.id} role="row">
                <td role="cell" data-label="Incident">
                  <Link to={`/incidents/${item.id}`}>{item.incidentNumber}</Link>
                </td>
                <td role="cell" data-label="Status">
                  <span className={`status status-${item.status.toLowerCase()}`}>{readableLabel(item.status)}</span>
                </td>
                <td role="cell" data-label="Complaint">{item.chiefComplaint}</td>
                <td role="cell" data-label="Agency">{item.agencyName}</td>
                <td role="cell" data-label="Report receipt"><span className={item.status === 'HandedOff' && item.receiptAcknowledged === false ? 'receipt-awaiting' : 'receipt-label'}>{receiptLabel(item.status, item.receiptAcknowledged)}</span></td>
                {!hospital && <td role="cell" data-label="Destination">{item.destinationHospitalName ?? 'Not selected'}</td>}
                <td role="cell" data-label="Updated">{new Date(item.updatedAt).toLocaleString()}</td>
              </tr>
            ))}
            {result.items.length === 0 && (
              <tr>
                <td colSpan={hospital ? 6 : 7}>{page > 1 ? 'No incidents on this page. Go to the previous page.' : queue === 'awaiting' ? 'No reports awaiting acknowledgment.' : queue !== 'all' ? 'No reports in this queue. Choose another queue or refresh for updates.' : 'No incidents yet.'}</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      )}
      <nav className="demo-row" aria-label="Incident pages">
        <button type="button" disabled={page === 1} onClick={() => setPage(value => value - 1)}>Previous</button>
        <span>Page {page}</span>
        <button type="button" disabled={loading || !!error || !result || page * result.pageSize >= result.totalCount}
          onClick={() => setPage(value => value + 1)}>Next</button>
      </nav>
    </section>
  );
}

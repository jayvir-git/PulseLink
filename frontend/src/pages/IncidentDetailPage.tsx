import { useEffect, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError, type IncidentDetail } from '../api/client';
import { useAuth } from '../auth/AuthContext';

type InterventionAttempt = { key: string; version: string; body: Record<string, string> };

export function IncidentDetailPage() {
  const { id } = useParams();
  // Router navigation between detail URLs can reuse the page component. Give
  // each incident its own drafts, retry identity, and outstanding async state.
  return id ? <IncidentDetailContent key={id} id={id} /> : null;
}

function IncidentDetailContent({ id }: { id: string }) {
  const { user } = useAuth();
  const [incident, setIncident] = useState<IncidentDetail | null>(null);
  const [error, setError] = useState('');
  const [conflict, setConflict] = useState(false);
  const [preserveDrafts, setPreserveDrafts] = useState(false);
  const [pending, setPending] = useState(false);
  const [notice, setNotice] = useState('');
  const [interventionAttempt, setInterventionAttempt] = useState<InterventionAttempt | null>(null);
  const [exportJson, setExportJson] = useState('');
  const [vitalForm, setVitalForm] = useState({
    heartRate: '88',
    systolicBp: '142',
    diastolicBp: '90',
    respiratoryRate: '18',
    spO2: '96',
    temperatureC: '37.0',
    glasgowComaScale: '15',
  });
  const [interventionForm, setInterventionForm] = useState({
    name: 'Aspirin',
    medication: 'Aspirin',
    dose: '324 mg',
    route: 'PO',
    notes: '',
  });

  const canEdit =
    !!incident &&
    incident.status !== 'HandedOff' &&
    (user?.role === 'Paramedic' || user?.role === 'Admin');

  async function reload() {
    if (!id) return;
    const data = await api.incident(id);
    setIncident(data);
    return data;
  }

  function showWriteError(err: unknown, fallback: string) {
    setError(err instanceof Error ? err.message : fallback);
    if (err instanceof ApiError && (err.status === 412 || err.status === 428 || err.code === 'incident_busy'
      || err.code === 'idempotency_key_expired' || err.code === 'idempotency_key_reused')) {
      setConflict(true);
      setPreserveDrafts(true);
    }
  }

  async function reloadForReview() {
    if (pending) return;
    setPending(true);
    try {
      const latest = await reload();
      setConflict(false);
      setError('');
      setNotice(latest?.status === 'HandedOff'
        ? 'Latest incident loaded. Your unsaved inputs are kept below.'
        : 'Latest incident loaded. Your unsaved inputs are kept below. Review the current records before submitting again.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not reload the incident');
    } finally {
      setPending(false);
    }
  }

  useEffect(() => {
    reload().catch((err) => setError(err instanceof Error ? err.message : 'Failed to load'));
  }, [id]);

  async function onTransition(toStatus: string) {
    if (!id || !incident || pending || conflict || interventionAttempt || !canEdit) return;
    setPending(true);
    setError('');
    setNotice('');
    try {
      setIncident(await api.transition(id, toStatus, incident.version));
    } catch (err) {
      showWriteError(err, 'Transition failed');
    } finally {
      setPending(false);
    }
  }

  async function onAddVital(e: FormEvent) {
    e.preventDefault();
    if (!id || !incident || pending || conflict || interventionAttempt || !canEdit) return;
    setPending(true);
    setError('');
    setNotice('');
    try {
      setIncident(
        await api.addVital(id, {
          heartRate: Number(vitalForm.heartRate) || null,
          systolicBp: Number(vitalForm.systolicBp) || null,
          diastolicBp: Number(vitalForm.diastolicBp) || null,
          respiratoryRate: Number(vitalForm.respiratoryRate) || null,
          spO2: Number(vitalForm.spO2) || null,
          temperatureC: Number(vitalForm.temperatureC) || null,
          glasgowComaScale: vitalForm.glasgowComaScale,
        }, incident.version),
      );
    } catch (err) {
      showWriteError(err, 'Failed to add vitals');
    } finally {
      setPending(false);
    }
  }

  async function onAddIntervention(e: FormEvent) {
    e.preventDefault();
    if (!id || !incident || pending || conflict || interventionAttempt || !canEdit) return;
    const attempt = { key: crypto.randomUUID(), version: incident.version, body: { ...interventionForm } };
    setInterventionAttempt(attempt);
    await submitIntervention(attempt);
  }

  async function submitIntervention(attempt: InterventionAttempt) {
    if (!id || pending) return;
    setPending(true);
    setError('');
    setNotice('');
    try {
      await api.addIntervention(id, attempt.body, attempt.version, attempt.key);
    } catch (err) {
      if (!(err instanceof ApiError) || err.status >= 500 || err.status === 408 || err.status === 429) {
        // The response may have been lost after commit. Keep this exact request
        // identity and payload; do not turn an uncertain result into a new action.
        setError('The intervention result is uncertain. Keep this page open and retry the same intervention.');
      } else {
        setInterventionAttempt(null);
        showWriteError(err, 'Failed to add intervention');
      }
      setPending(false);
      return;
    }

    setInterventionAttempt(null);
    setNotice('Intervention recorded. Adding again will create a separate intervention.');
    try {
      await reload();
    } catch {
      setError('The intervention was recorded, but the latest incident could not be loaded. Reload before continuing.');
      setConflict(true);
      setPreserveDrafts(true);
    } finally {
      setPending(false);
    }
  }

  async function onExport() {
    if (!id || pending) return;
    setPending(true);
    setError('');
    try {
      const payload = await api.exportHandoff(id);
      setExportJson(JSON.stringify(payload, null, 2));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Export failed');
    } finally {
      setPending(false);
    }
  }

  if (!incident) {
    return (
      <section className="page">
        {error ? <p className="error">{error}</p> : <p>Loading…</p>}
      </section>
    );
  }

  return (
    <section className="page">
      <p>
        <Link to="/">← Back to list</Link>
      </p>
      <header className="page-header">
        <div>
          <p className="eyebrow">{incident.incidentNumber}</p>
          <h1>{incident.chiefComplaint}</h1>
          <p className="lede">
            {incident.agencyName}
            {incident.destinationHospitalName ? ` → ${incident.destinationHospitalName}` : ''}
          </p>
        </div>
        <span className={`status status-${incident.status.toLowerCase()}`}>{incident.status}</span>
      </header>

      {error && <p className="error">{error}</p>}
      {interventionAttempt && (
        <div role="alert">
          <p>{pending ? 'Saving intervention…' : 'Retry uses the same request and cannot add a second copy. The original inputs are kept below.'}</p>
          <button type="button" disabled={pending} onClick={() => submitIntervention(interventionAttempt)}>Retry same intervention</button>
        </div>
      )}
      {conflict && (
        <div role="alert">
          <p>Your unsaved inputs are kept. Reload the latest incident and review it before submitting again.</p>
          <button type="button" disabled={pending} onClick={reloadForReview}>Reload latest incident</button>
        </div>
      )}
      {notice && <p role="status">{notice}</p>}
      {preserveDrafts && !canEdit && <p>This incident has been handed off. Your retained inputs cannot be submitted.</p>}

      <div className="detail-grid">
        <div className="panel">
          <h2>Clinical summary</h2>
          <dl className="meta">
            <div>
              <dt>Age range</dt>
              <dd>{incident.patientAgeRange ?? '—'}</dd>
            </div>
            <div>
              <dt>Sex</dt>
              <dd>{incident.patientSex ?? '—'}</dd>
            </div>
            <div>
              <dt>Notes</dt>
              <dd>{incident.notes || '—'}</dd>
            </div>
          </dl>

          {canEdit && incident.allowedNextStatuses.length > 0 && (
            <div className="actions">
              <h3>Advance status</h3>
              <div className="demo-row">
                {incident.allowedNextStatuses.map((status) => (
                  <button key={status} type="button" disabled={pending || conflict || !!interventionAttempt} onClick={() => onTransition(status)}>
                    Mark {status}
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="actions">
            <h3>Hospital handoff export</h3>
            <button type="button" className="ghost" disabled={pending} onClick={onExport}>
              Generate FHIR-like JSON
            </button>
            {exportJson && <pre className="export">{exportJson}</pre>}
          </div>
        </div>

        <div className="panel">
          <h2>Vitals</h2>
          <ul className="list">
            {incident.vitalSigns.map((v) => (
              <li key={v.id}>
                <strong>{new Date(v.recordedAt).toLocaleString()}</strong>
                <span>
                  HR {v.heartRate ?? '—'} · BP {v.systolicBp ?? '—'}/{v.diastolicBp ?? '—'} · RR{' '}
                  {v.respiratoryRate ?? '—'} · SpO2 {v.spO2 ?? '—'}% · GCS {v.glasgowComaScale ?? '—'}
                </span>
              </li>
            ))}
            {incident.vitalSigns.length === 0 && <li>No vitals recorded.</li>}
          </ul>
          {(canEdit || preserveDrafts) && (
            <form className="stack tight" onSubmit={onAddVital}>
              <fieldset className="stack tight" disabled={!canEdit || pending || conflict || !!interventionAttempt} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
                <div className="grid-3">
                  {Object.entries(vitalForm).map(([key, value]) => (
                    <label key={key}>
                      {key}
                      <input
                        value={value}
                        onChange={(e) => setVitalForm((prev) => ({ ...prev, [key]: e.target.value }))}
                      />
                    </label>
                  ))}
                </div>
                <button type="submit">Add vitals</button>
              </fieldset>
            </form>
          )}
        </div>

        <div className="panel">
          <h2>Interventions</h2>
          <ul className="list">
            {incident.interventions.map((i) => (
              <li key={i.id}>
                <strong>{i.name}</strong>
                <span>
                  {i.medication} {i.dose} {i.route} · {new Date(i.performedAt).toLocaleString()}
                </span>
              </li>
            ))}
            {incident.interventions.length === 0 && <li>No interventions recorded.</li>}
          </ul>
          {(canEdit || preserveDrafts) && (
            <form className="stack tight" onSubmit={onAddIntervention}>
              <fieldset className="stack tight" disabled={!canEdit || pending || conflict || !!interventionAttempt} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
                <div className="grid-2">
                  {Object.entries(interventionForm).map(([key, value]) => (
                    <label key={key}>
                      {key}
                      <input
                        value={value}
                        onChange={(e) =>
                          setInterventionForm((prev) => ({ ...prev, [key]: e.target.value }))
                        }
                      />
                    </label>
                  ))}
                </div>
                <button type="submit">Add intervention</button>
              </fieldset>
            </form>
          )}
        </div>

        <div className="panel">
          <h2>Audit trail</h2>
          <ul className="list">
            {incident.auditEvents.map((a) => (
              <li key={a.id}>
                <strong>{a.action}</strong>
                <span>
                  {a.details} · {new Date(a.createdAt).toLocaleString()}
                </span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

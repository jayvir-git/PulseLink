import { useEffect, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, type IncidentDetail } from '../api/client';
import { useAuth } from '../auth/AuthContext';

export function IncidentDetailPage() {
  const { id } = useParams();
  const { user } = useAuth();
  const [incident, setIncident] = useState<IncidentDetail | null>(null);
  const [error, setError] = useState('');
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
  }

  useEffect(() => {
    reload().catch((err) => setError(err instanceof Error ? err.message : 'Failed to load'));
  }, [id]);

  async function onTransition(toStatus: string) {
    if (!id) return;
    setError('');
    try {
      setIncident(await api.transition(id, toStatus));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Transition failed');
    }
  }

  async function onAddVital(e: FormEvent) {
    e.preventDefault();
    if (!id) return;
    setError('');
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
        }),
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add vitals');
    }
  }

  async function onAddIntervention(e: FormEvent) {
    e.preventDefault();
    if (!id) return;
    setError('');
    try {
      setIncident(await api.addIntervention(id, interventionForm));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add intervention');
    }
  }

  async function onExport() {
    if (!id) return;
    setError('');
    try {
      const payload = await api.exportHandoff(id);
      setExportJson(JSON.stringify(payload, null, 2));
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Export failed');
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
                  <button key={status} type="button" onClick={() => onTransition(status)}>
                    Mark {status}
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="actions">
            <h3>Hospital handoff export</h3>
            <button type="button" className="ghost" onClick={onExport}>
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
          {canEdit && (
            <form className="stack tight" onSubmit={onAddVital}>
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
          {canEdit && (
            <form className="stack tight" onSubmit={onAddIntervention}>
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

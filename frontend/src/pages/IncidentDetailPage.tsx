import { NARRATIVE_LIMIT } from '../api/limits';
import { useEffect, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError, type IncidentDetail } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { readableLabel } from '../ui';
import { HospitalHandoff } from './HospitalHandoff';

const vitalLabels: Record<string, string> = { heartRate: 'Heart rate (bpm)', systolicBp: 'Systolic BP (mmHg)', diastolicBp: 'Diastolic BP (mmHg)', respiratoryRate: 'Respiratory rate (/min)', spO2: 'Oxygen saturation (%)', temperatureC: 'Temperature (°C)', glasgowComaScale: 'Glasgow Coma Scale' };
const interventionLabels: Record<string, string> = { name: 'Intervention name', medication: 'Medication', dose: 'Dose', route: 'Route', notes: 'Intervention notes' };

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
  const [feedbackSection, setFeedbackSection] = useState('');
  const [interventionAttempt, setInterventionAttempt] = useState<InterventionAttempt | null>(null);
  const [exportJson, setExportJson] = useState('');
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [hospitals, setHospitals] = useState<{ id: string; name: string }[]>([]);
  const [destination, setDestination] = useState('');
  const [editingDestination, setEditingDestination] = useState(false);
  const [lookupError, setLookupError] = useState('');
  const [refreshError, setRefreshError] = useState('');
  const [notesDraft, setNotesDraft] = useState<string | null>(null);

  const [vitalForm, setVitalForm] = useState({
    heartRate: '',
    systolicBp: '',
    diastolicBp: '',
    respiratoryRate: '',
    spO2: '',
    temperatureC: '',
    glasgowComaScale: '',
  });
  const [interventionForm, setInterventionForm] = useState({
    name: '',
    medication: '',
    dose: '',
    route: '',
    notes: '',
  });

  const canEdit =
    !!incident &&
    incident.status !== 'HandedOff' &&
    (user?.role === 'Paramedic' || user?.role === 'Admin');

  const hasVitalDraft = Object.values(vitalForm).some(value => value.trim());
  const hasTreatmentDraft = Object.values(interventionForm).some(value => value.trim());
  const hasNotesDraft = notesDraft !== null && notesDraft !== (incident?.notes ?? '');
  const notesTooLong = (notesDraft ?? incident?.notes ?? "").length > NARRATIVE_LIMIT;
  const hasDrafts = hasVitalDraft || hasTreatmentDraft || hasNotesDraft || editingDestination;

  const showDraftInputs = canEdit || preserveDrafts || hasDrafts;

  useEffect(() => {
    if (!hasDrafts && !pending && !interventionAttempt) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ''; };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [hasDrafts, pending, interventionAttempt]);

  async function saveNotes(e: FormEvent) {
    setFeedbackSection('care-notes');
    e.preventDefault();
    if (!incident || !canEdit || pending || conflict || interventionAttempt || notesDraft === null || notesTooLong) return;
    setPending(true); setError(''); setNotice('');
    try {
      setIncident(await api.updateIncident(id, {
        chiefComplaint: incident.chiefComplaint,
        patientAgeRange: incident.patientAgeRange ?? undefined,
        patientSex: incident.patientSex ?? undefined,
        destinationHospitalId: incident.destinationHospitalId ?? undefined,
        notes: notesDraft,
      }, incident.version));
      setNotesDraft(null);
      setNotice('Care notes saved. The receiving team sees this narrative with the report.');
    } catch (err) { showWriteError(err, 'Could not save care notes'); }
    finally { setPending(false); }
  }

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
    setFeedbackSection('');
    if (pending) return;
    setPending(true);
    try {
      const latest = await reload();
      setConflict(false);
      setError('');
      setNotice(!hasDrafts ? 'Report refreshed.' : latest?.status === 'HandedOff'
        ? 'Latest incident loaded. Your unsaved inputs are kept below.'
        : 'Latest incident loaded. Your unsaved inputs are kept below. Review the current records before submitting again.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not reload the incident');
    } finally {
      setPending(false);
    }
  }

  useEffect(() => {
    let active = true;
    setError('');
    api.incident(id).then(data => { if (active) setIncident(data); })
      .catch(err => { if (active) setError(err instanceof Error ? err.message : 'Failed to load'); });
    return () => { active = false; };
  }, [id, loadAttempt]);

  async function editDestination() {
    setEditingDestination(true);
    setDestination(incident?.destinationHospitalId ?? '');
    setLookupError('');
    try { setHospitals(await api.hospitals()); }
    catch (err) { setLookupError(err instanceof Error ? err.message : 'Could not load hospitals. Try again.'); }
  }

  async function saveDestination(e: FormEvent) {
    setFeedbackSection('summary');
    e.preventDefault();
    if (!incident || !canEdit || pending || conflict || interventionAttempt || !destination) return;
    setPending(true);
    setError('');
    setNotice('');
    try {
      setIncident(await api.updateIncident(id, {
        chiefComplaint: incident.chiefComplaint,
        patientAgeRange: incident.patientAgeRange ?? undefined,
        patientSex: incident.patientSex ?? undefined,
        notes: incident.notes ?? undefined,
        destinationHospitalId: destination,
      }, incident.version));
      setEditingDestination(false);
      setNotice('Destination saved. The receiving hospital can see this report once transport starts.');
    } catch (err) { showWriteError(err, 'Could not save destination'); }
    finally { setPending(false); }
  }

  async function onTransition(toStatus: string) {
    setFeedbackSection('summary');
    if (!id || !incident || pending || conflict || interventionAttempt || !canEdit) return;
    if (toStatus === 'HandedOff' && hasDrafts) {
      setError('Save or clear your unsaved entries before handing off. Clinical editing closes after handoff.');
      return;
    }
    setPending(true);
    setError('');
    setNotice('');
    try {
      setIncident(await api.transition(id, toStatus, incident.version));
      setNotice(`Status updated to ${readableLabel(toStatus)}.`);
    } catch (err) {
      showWriteError(err, 'Transition failed');
    } finally {
      setPending(false);
    }
  }

  async function onAddVital(e: FormEvent) {
    setFeedbackSection('vitals');
    e.preventDefault();
    if (!id || !incident || pending || conflict || interventionAttempt || !canEdit) return;
    if (!Object.values(vitalForm).some(value => value.trim())) {
      setError('Enter at least one observation before adding vitals.');
      return;
    }
    setPending(true);
    setError('');
    setNotice('');
    try {
      setIncident(
        await api.addVital(id, {
          heartRate: vitalForm.heartRate.trim() === '' ? null : Number(vitalForm.heartRate),
          systolicBp: vitalForm.systolicBp.trim() === '' ? null : Number(vitalForm.systolicBp),
          diastolicBp: vitalForm.diastolicBp.trim() === '' ? null : Number(vitalForm.diastolicBp),
          respiratoryRate: vitalForm.respiratoryRate.trim() === '' ? null : Number(vitalForm.respiratoryRate),
          spO2: vitalForm.spO2.trim() === '' ? null : Number(vitalForm.spO2),
          temperatureC: vitalForm.temperatureC.trim() === '' ? null : Number(vitalForm.temperatureC),
          glasgowComaScale: vitalForm.glasgowComaScale,
        }, incident.version),
      );
      setVitalForm({ heartRate: '', systolicBp: '', diastolicBp: '', respiratoryRate: '', spO2: '', temperatureC: '', glasgowComaScale: '' });
      setNotice('Vitals recorded. The form is ready for the next observation.');
    } catch (err) {
      showWriteError(err, 'Failed to add vitals');
    } finally {
      setPending(false);
    }
  }

  async function onAddIntervention(e: FormEvent) {
    setFeedbackSection('interventions');
    e.preventDefault();
    if (!id || !incident || pending || conflict || interventionAttempt || !canEdit) return;
    const attempt = { key: crypto.randomUUID(), version: incident.version, body: { ...interventionForm } };
    setInterventionAttempt(attempt);
    await submitIntervention(attempt);
  }

  async function submitIntervention(attempt: InterventionAttempt) {
    setFeedbackSection('interventions');
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
    setInterventionForm({ name: '', medication: '', dose: '', route: '', notes: '' });
    setNotice('Intervention recorded. The form is ready for the next treatment.');
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

  async function onAcknowledge() {
    if (!incident || pending) return;
    setPending(true);
    setError('');
    setNotice('');
    try {
      const receipt = await api.acknowledgeReceipt(id);
      setIncident({ ...incident, auditEvents: [receipt, ...incident.auditEvents.filter(a => a.id !== receipt.id)] });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not confirm receipt. You can retry.');
    } finally {
      setPending(false);
    }
  }

  async function onExport() {
    setFeedbackSection('summary');
    if (!id || pending) return;
    setPending(true);
    setError('');
    setNotice('');
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
        <p><Link to="/">← Back to list</Link></p>
        {error ? <><p className="error" role="alert">{error}</p><button onClick={() => setLoadAttempt(value => value + 1)}>Retry incident</button></> : <p role="status">Loading…</p>}
      </section>
    );
  }

  if (user?.role === 'HospitalStaff') return <HospitalHandoff
    incident={incident} canAcknowledge={user.hospitalId === incident.destinationHospitalId}
    pending={pending} error={error} refreshError={refreshError} notice={notice} exportJson={exportJson}
    onAcknowledge={onAcknowledge} onExport={onExport}
    onRefresh={async () => {
      if (pending) return;
      setPending(true);
      setError('');
      setNotice('');
      try { await reload(); setRefreshError(''); setNotice('Report refreshed.'); }
      catch (err) { setRefreshError(`Could not refresh. Showing the previously loaded report. ${err instanceof Error ? err.message : 'Try again.'}`); }
      finally { setPending(false); }
    }}
  />;

  const feedback = (section: string) => feedbackSection === section && <div className="section-feedback">
    {pending && <p role="status">Saving / loading…</p>}
    {!pending && notice && <p role="status">{notice}</p>}
    {error && !conflict && !interventionAttempt && <p className="error" role="alert">{error}</p>}
  </div>;
  const receipt = incident.auditEvents.find(a => a.action === 'HandoffAcknowledged');
  return (
    <section className="page receiving-report field-report">
      <div className="report-toolbar"><Link to="/">← Back to list</Link>{!canEdit && <button className="ghost" disabled={pending} onClick={reloadForReview}>Refresh report</button>}</div>
      <header className="report-heading">
        <div className="report-kicker"><span>FIELD CARE RECORD · {incident.incidentNumber}</span><span className={`status status-${incident.status.toLowerCase()}`}>{readableLabel(incident.status)}</span></div>
        <h1>{incident.chiefComplaint}</h1>
        <dl className="patient-context">
          <div><dt>Age range</dt><dd>{incident.patientAgeRange || 'Not recorded'}</dd></div>
          <div><dt>Sex</dt><dd>{incident.patientSex || 'Not recorded'}</dd></div>
          <div><dt>Destination</dt><dd>{incident.destinationHospitalName || 'Not selected'}</dd></div>
        </dl>
        <div className="report-provenance"><span>{incident.agencyName}</span><span>Report updated {new Date(incident.updatedAt).toLocaleString()}</span></div>
      </header>

      {error && (!feedbackSection || conflict || interventionAttempt) && <p className="error" role={conflict || interventionAttempt ? undefined : 'alert'}>{error}</p>}
      <nav className="report-nav" aria-label="Report sections"><a href="#vitals">Observations</a><a href="#interventions">Treatments</a><a href="#care-notes">Care notes</a><a href="#summary">Transport & handoff</a></nav>
      <div className="field-save-state">{pending ? 'Saving / loading…' : interventionAttempt ? 'Treatment save uncertain — retry the same entry below' : hasDrafts && !canEdit ? 'Unsaved entries retained — clinical editing is closed' : hasDrafts ? 'Unsaved entries — save each section before leaving this report' : canEdit ? 'Ready to document · entries save when you submit each section' : 'Handed off · clinical record is read-only'}</div>
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
      {!feedbackSection && notice && <p role="status">{notice}</p>}
      {showDraftInputs && !canEdit && <p>This incident has been handed off. Your retained inputs cannot be submitted.</p>}

      <p className="report-caption field-time-note">Recorded times reflect entry time, not verified care time. Unsaved entries are lost when you leave this page.</p>
      <div className="field-layout">
        <div className="clinical-report-body">
        <div className="report-section" id="vitals">
          <div className="report-section-heading"><h2>Observations</h2><span>{hasVitalDraft ? 'Unsaved observation' : canEdit ? 'New observation set' : 'Saved observations'}</span></div>
          {canEdit && <p className="form-hint">Enter measured observations. Leave unrecorded values blank; — in saved records means not recorded.</p>}
          {showDraftInputs && (
            <form className="stack tight" onSubmit={onAddVital}>
              <fieldset className="stack tight" disabled={!canEdit || pending || conflict || !!interventionAttempt} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
                <div className="grid-3">
                  {Object.entries(vitalForm).map(([key, value]) => (
                    <label key={key}>
                      {vitalLabels[key]}
                      <input
                        type="number"
                        inputMode={key === 'temperatureC' ? 'decimal' : 'numeric'}
                        step={key === 'temperatureC' ? '0.1' : '1'}
                        value={value}
                        onChange={(e) => setVitalForm((prev) => ({ ...prev, [key]: e.target.value }))}
                      />
                    </label>
                  ))}
                </div>
                <div className="field-form-actions"><button type="submit">Add vitals</button><button type="button" className="ghost" disabled={!hasVitalDraft} onClick={() => setVitalForm({ heartRate: '', systolicBp: '', diastolicBp: '', respiratoryRate: '', spO2: '', temperatureC: '', glasgowComaScale: '' })}>Clear observation inputs</button></div>
              </fieldset>
            </form>
          )}
          {feedback('vitals')}
          <details className="field-history" open>
            <summary>Saved observation sets</summary>
          <ul className="list">
            {[...incident.vitalSigns].sort((a, b) => Date.parse(b.recordedAt) - Date.parse(a.recordedAt)).map((v) => (
              <li key={v.id}>
                <strong>Recorded {new Date(v.recordedAt).toLocaleString()}</strong>
                <span>
                  HR {v.heartRate ?? '—'} · BP {v.systolicBp ?? '—'}/{v.diastolicBp ?? '—'} · RR{' '}
                  {v.respiratoryRate ?? '—'}/min · SpO₂ {v.spO2 ?? '—'}% · Temp {v.temperatureC ?? '—'}°C · GCS {v.glasgowComaScale ?? '—'}
                </span>
              </li>
            ))}
            {incident.vitalSigns.length === 0 && <li>No vitals recorded.</li>}
          </ul>
          </details>
        </div>

        <div className="report-section" id="interventions">
          <div className="report-section-heading"><h2>Treatments</h2><span>{hasTreatmentDraft ? 'Unsaved treatment' : canEdit ? 'New intervention' : 'Saved treatments'}</span></div>
          {canEdit && <p className="form-hint">Record the action taken, medication details if applicable, and the response or complications in intervention notes.</p>}
          {showDraftInputs && (
            <form className="stack tight" onSubmit={onAddIntervention}>
              <fieldset className="stack tight" disabled={!canEdit || pending || conflict || !!interventionAttempt} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
                <div className="grid-2">
                  {Object.entries(interventionForm).map(([key, value]) => (
                    <label key={key} className={key === 'notes' || key === 'name' ? 'field-wide' : undefined}>
                      {interventionLabels[key]}
                      {key === 'notes' ? <textarea aria-label="Intervention notes" rows={3} value={value} onChange={e => setInterventionForm(prev => ({ ...prev, notes: e.target.value }))} /> : <input
                        required={key === 'name'}
                        value={value}
                        onChange={(e) =>
                          setInterventionForm((prev) => ({ ...prev, [key]: e.target.value }))
                        }
                      />}
                    </label>
                  ))}
                </div>
                <div className="field-form-actions"><button type="submit">Add intervention</button><button type="button" className="ghost" disabled={!hasTreatmentDraft} onClick={() => setInterventionForm({ name: '', medication: '', dose: '', route: '', notes: '' })}>Clear treatment inputs</button></div>
              </fieldset>
            </form>
          )}
          {feedback('interventions')}
          <details className="field-history" open>
            <summary>Saved treatments</summary>
          <ul className="list">
            {[...incident.interventions].sort((a, b) => Date.parse(b.performedAt) - Date.parse(a.performedAt)).map((i) => (
              <li key={i.id}>
                <strong>{i.name}</strong>
                <span>
                  {[i.medication, i.dose, i.route].filter(Boolean).join(' ')}{(i.medication || i.dose || i.route) && ' · '}Recorded {new Date(i.performedAt).toLocaleString()}
                </span>
                {i.notes && <p className="treatment-note">{i.notes}</p>}
              </li>
            ))}
            {incident.interventions.length === 0 && <li>No interventions recorded.</li>}
          </ul>
          </details>
        </div>

        <section className="report-section" id="care-notes">
          <div className="report-section-heading"><h2>Care notes</h2><span>{hasNotesDraft ? 'Unsaved changes' : 'Saved narrative'}</span></div>
          <p className="form-hint">Document the presenting problem, onset, relevant history, assessment, changes and response to care. Include actual event times here when needed. Distinguish unknown information from confirmed findings.</p>
          {showDraftInputs ? <form className="stack" onSubmit={saveNotes}>
            <label>EMS narrative<textarea aria-label="EMS narrative" aria-describedby="narrative-limit" aria-invalid={notesTooLong} rows={6} value={notesDraft ?? incident.notes ?? ''} disabled={!canEdit || pending || conflict || !!interventionAttempt} onChange={e => setNotesDraft(e.target.value)} /></label>
            <p id="narrative-limit" className={notesTooLong ? "error" : "form-hint"}>{(notesDraft ?? incident.notes ?? "").length} / {NARRATIVE_LIMIT} characters.{notesTooLong && " Shorten the narrative before saving; your text has been kept."}</p>
            <div className="field-form-actions"><button disabled={notesTooLong || !canEdit || !hasNotesDraft || pending || conflict || !!interventionAttempt}>Save care notes</button><button type="button" className="ghost" disabled={!canEdit || !hasNotesDraft || pending || conflict || !!interventionAttempt} onClick={() => setNotesDraft(null)}>Discard note changes</button></div>
            {hasNotesDraft && <details><summary>Compare with saved care notes</summary><p className="ems-narrative">{incident.notes || 'Not recorded'}</p></details>}
          </form> : <p className="ems-narrative">{incident.notes || 'Not recorded'}</p>}
          {feedback('care-notes')}
        </section>
        </div>
        <aside className="receiving-sidebar">
        <div className="report-section field-transport" id="summary">
          <h2>Transport & handoff</h2>
          <p className="report-caption">Current status: <strong>{readableLabel(incident.status)}</strong></p>
          <p className="report-caption">Saved record: {incident.vitalSigns.length} {incident.vitalSigns.length === 1 ? 'observation set' : 'observation sets'} · {incident.interventions.length} {incident.interventions.length === 1 ? 'treatment' : 'treatments'} · {incident.notes?.trim() ? 'care notes recorded' : 'no care notes'}. Review saved care before closing clinical editing.</p>
          {canEdit && <div className="actions destination-editor">
            <h3>Destination hospital</h3>
            <p>{incident.destinationHospitalName ?? 'Choose a destination before starting transport.'}</p>
            {!editingDestination ? <button className="ghost" disabled={pending || conflict || !!interventionAttempt} onClick={editDestination}>{incident.destinationHospitalId ? 'Change destination' : 'Choose destination'}</button> : <form className="stack" onSubmit={saveDestination}>
              <label>Receiving hospital<select aria-label="Receiving hospital" value={destination} required disabled={pending || conflict || !!interventionAttempt} onChange={e => setDestination(e.target.value)}><option value="">Select hospital</option>{hospitals.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}</select></label>
              {lookupError && <p className="error" role="alert">{lookupError} <button type="button" className="ghost" onClick={editDestination}>Retry hospitals</button></p>}
              <div className="demo-row"><button disabled={!destination || pending || conflict || !!interventionAttempt}>Save destination</button><button type="button" className="ghost" disabled={pending} onClick={() => setEditingDestination(false)}>Cancel</button></div>
            </form>}
          </div>}

          {incident.status === 'HandedOff' && (
            <div className={`actions receipt-panel ${receipt ? 'received' : ''}`}>
              <h3>Hospital receipt</h3>
              <strong>{receipt ? 'Report received' : 'EMS handoff complete · awaiting receipt'}</strong>
              <p className="form-hint">Confirms delivery of this report, not transfer of clinical responsibility.</p>
              <p role="status">{receipt
                ? `${receipt.details} · ${new Date(receipt.createdAt).toLocaleString()}`
                : 'Awaiting acknowledgment from the receiving hospital.'}</p>
              {!receipt && user?.role === 'HospitalStaff' && user.hospitalId === incident.destinationHospitalId && (
                <button type="button" disabled={pending} onClick={onAcknowledge}>Acknowledge receipt</button>
              )}
            </div>
          )}
          {canEdit && incident.allowedNextStatuses.length > 0 && (
            <div className="actions">
              <h3>Next transport step</h3>
              <div className="demo-row">
                {incident.allowedNextStatuses.map((status) => (
                  <button key={status} type="button" disabled={pending || conflict || !!interventionAttempt || (!incident.destinationHospitalId && ['Transporting', 'Arrived', 'HandedOff'].includes(status))} onClick={() => onTransition(status)}>
                    Mark {readableLabel(status)}
                  </button>
                ))}
              </div>
              {!incident.destinationHospitalId && <p className="form-hint">A destination is required before transport. Choose one above.</p>}
              {incident.allowedNextStatuses.includes('HandedOff') && <p className="form-hint">Handing off closes clinical editing. Review saved observations, treatments and care notes, and complete the verbal handoff. Unsaved entries must be saved or cleared first.</p>}
            </div>
          )}

          {feedback('summary')}
          <details className="actions"><summary>Handoff export</summary>
            <button type="button" className="ghost" disabled={pending} onClick={onExport}>
              Generate FHIR-like JSON
            </button>
            {exportJson && <pre className="export">{exportJson}</pre>}
          </details>
        </div>

        <details className="report-section" id="audit">
          <summary>Activity log</summary>
          <ul className="list">
            {incident.auditEvents.map((a) => (
              <li key={a.id}>
                <strong>{readableLabel(a.action)}</strong>
                <span>
                  {a.details} · {new Date(a.createdAt).toLocaleString()}
                </span>
              </li>
            ))}
          </ul>
        </details>
        </aside>
      </div>
    </section>
  );
}

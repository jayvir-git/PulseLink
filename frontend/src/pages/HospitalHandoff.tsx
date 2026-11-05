import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { IncidentDetail } from '../api/client';
import { readableLabel } from '../ui';

type Props = {
  incident: IncidentDetail;
  canAcknowledge: boolean;
  pending: boolean;
  error: string;
  refreshError: string;
  notice: string;
  exportJson: string;
  onRefresh: () => Promise<void>;
  onAcknowledge: () => Promise<void>;
  onExport: () => Promise<void>;
};

const recorded = (value: string | number | null | undefined) =>
  value === null || value === undefined || value === '' ? 'Not recorded' : String(value);

function ReportTime({ value }: { value: string }) {
  return <time dateTime={value}>{new Date(value).toLocaleString(undefined, {
    month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit',
  })}</time>;
}

export function HospitalHandoff({ incident, canAcknowledge, pending, error, refreshError, notice, exportJson, onRefresh, onAcknowledge, onExport }: Props) {
  const [action, setAction] = useState('');
  const observations = [...incident.vitalSigns].sort((a, b) => Date.parse(b.recordedAt) - Date.parse(a.recordedAt));
  const latest = observations[0];
  const treatments = [...incident.interventions].sort((a, b) => Date.parse(b.performedAt) - Date.parse(a.performedAt));
  const receipt = incident.auditEvents.find(event => event.action === 'HandoffAcknowledged');
  const handedOff = incident.status === 'HandedOff';
  const metrics = [
    { label: 'Heart rate', value: recorded(latest?.heartRate), unit: 'bpm' },
    { label: 'Blood pressure', value: latest?.systolicBp == null && latest?.diastolicBp == null ? 'Not recorded' : `${recorded(latest?.systolicBp)} / ${recorded(latest?.diastolicBp)}`, unit: 'mmHg' },
    { label: 'Respiratory rate', value: recorded(latest?.respiratoryRate), unit: '/min' },
    { label: 'Oxygen saturation', value: recorded(latest?.spO2), unit: '%' },
    { label: 'Temperature', value: recorded(latest?.temperatureC), unit: '°C' },
    { label: 'Glasgow Coma Scale', value: recorded(latest?.glasgowComaScale), unit: '' },
  ];

  async function run(label: string, task: () => Promise<void>) {
    if (pending || action) return;
    setAction(label);
    try { await task(); }
    finally { setAction(''); }
  }

  return <section className="page receiving-report" aria-label="Hospital handoff report">
    <div className="report-toolbar">
      <Link to="/">← Incoming handoffs</Link>
      <button type="button" className="ghost" disabled={pending || !!action} onClick={() => run('Refreshing report…', onRefresh)}>Refresh report</button>
    </div>

    <header className="report-heading">
      <div className="report-kicker"><span>EMS patient report</span><span className={`status status-${incident.status.toLowerCase()}`}>{readableLabel(incident.status)}</span></div>
      <h1>{incident.chiefComplaint}</h1>
      <dl className="patient-context">
        <div><dt>Report</dt><dd>{incident.incidentNumber}</dd></div>
        <div><dt>Age range</dt><dd>{recorded(incident.patientAgeRange)}</dd></div>
        <div><dt>Sex</dt><dd>{recorded(incident.patientSex)}</dd></div>
      </dl>
      <div className="report-provenance"><span>{incident.agencyName} → <strong>{incident.destinationHospitalName ?? 'Destination not recorded'}</strong></span><span>Report updated <ReportTime value={incident.updatedAt} /></span></div>
    </header>

    <nav className="report-nav" aria-label="Handoff report sections"><a href="#observations">Observations</a><a href="#treatments">Treatments</a><a href="#field-notes">Care notes</a><a href="#report-receipt">Receipt</a></nav>
    <div role="status" className="report-feedback">{action || (!error && notice)}</div>
    {error && <p role="alert" className="error">{error}</p>}
    {refreshError && <p role="alert" className="error">{refreshError}</p>}

    <section className="report-section observations-section" id="observations" aria-labelledby="observations-title">
      <div className="report-section-heading"><h2 id="observations-title">Latest observations</h2><span>{latest ? <>Recorded <ReportTime value={latest.recordedAt} /></> : 'No observations recorded'}</span></div>
      <dl className="observation-grid">{metrics.map(metric => <div key={metric.label}>
        <dt>{metric.label}</dt><dd className={metric.value.includes('Not recorded') ? 'measurement-missing' : ''}>{metric.value}{metric.value !== 'Not recorded' && metric.unit && <small>{metric.unit}</small>}</dd>
      </div>)}</dl>
      <p className="report-caption">{observations.length ? 'Values are from this observation set only. Earlier readings are not substituted for missing values.' : 'No measurements have been submitted for this report.'} Recorded times reflect entry time, not verified care time, and are shown in your local time zone.</p>
      {observations.length > 1 && <details className="observation-history"><summary>All observation sets ({observations.length})</summary>
        <div className="observation-table-scroll" role="region" aria-label="Observation history, scroll horizontally for all measurements" tabIndex={0}><table>
          <caption>Recorded observations, newest first. “Not recorded” means no value in that set.</caption>
          <thead><tr><th scope="col">Recorded</th><th scope="col">HR (bpm)</th><th scope="col">BP (mmHg)</th><th scope="col">RR (/min)</th><th scope="col">SpO₂ (%)</th><th scope="col">Temp (°C)</th><th scope="col">GCS</th></tr></thead>
          <tbody>{observations.map(v => <tr key={v.id}><th scope="row"><ReportTime value={v.recordedAt} /></th><td>{recorded(v.heartRate)}</td><td>{recorded(v.systolicBp)} / {recorded(v.diastolicBp)}</td><td>{recorded(v.respiratoryRate)}</td><td>{recorded(v.spO2)}</td><td>{recorded(v.temperatureC)}</td><td>{recorded(v.glasgowComaScale)}</td></tr>)}</tbody>
        </table></div>
      </details>}
    </section>

    <div className="receiving-columns">
      <div className="clinical-report-body">
        <section className="report-section" id="treatments" aria-labelledby="treatments-title">
          <div className="report-section-heading"><h2 id="treatments-title">Treatments & interventions</h2><span>{treatments.length} recorded · newest first</span></div>
          {treatments.length ? <ol className="treatment-list">{treatments.map(t => <li key={t.id}>
            <div className="treatment-title"><h3>{t.name}</h3><span className="report-caption">Recorded <ReportTime value={t.performedAt} /></span></div>
            {(t.medication || t.dose || t.route) && <dl className="treatment-details"><div><dt>Medication</dt><dd>{recorded(t.medication)}</dd></div><div><dt>Dose</dt><dd>{recorded(t.dose)}</dd></div><div><dt>Route</dt><dd>{recorded(t.route)}</dd></div></dl>}
            <p className="treatment-note"><span>EMS note</span> {t.notes?.trim() ? t.notes : 'No intervention note recorded.'}</p>
          </li>)}</ol> : <p className="report-empty">No interventions recorded in this report.</p>}
        </section>
        <section className="report-section" id="field-notes" aria-labelledby="notes-title">
          <div className="report-section-heading"><h2 id="notes-title">Care notes</h2></div>
          <p className="ems-narrative">{incident.notes?.trim() ? incident.notes : 'No narrative recorded in this report.'}</p>
          <details className="report-gaps"><summary>Information this report does not collect</summary><p>Patient name and date of birth, allergies, usual medications, medical history, symptom onset, and ETA have no dedicated fields in PulseLink. Relevant details may appear in EMS notes; their absence does not mean “none.” Confirm missing information with the EMS team.</p></details>
        </section>
      </div>
      <aside className="receiving-sidebar" aria-label="Handoff coordination">
        <section className="report-section receipt-summary" id="report-receipt" aria-labelledby="receipt-title">
          <h2 id="receipt-title">Report receipt</h2>
          <p className="receipt-state">{receipt ? 'Receipt acknowledged' : handedOff ? 'Awaiting hospital acknowledgment' : 'EMS handoff not complete'}</p>
          {receipt ? <p role="status">{receipt.details}<br /><ReportTime value={receipt.createdAt} /></p> : <p>{handedOff ? 'Review the report and confirm that your team has received it.' : <>EMS last reported: {readableLabel(incident.status)}. You can review this report now; acknowledgment becomes available after EMS completes handoff.</>}</p>}
          {!receipt && handedOff && canAcknowledge && <button type="button" disabled={pending || !!action} onClick={() => run('Acknowledging receipt…', onAcknowledge)}>Acknowledge receipt</button>}
          <p className="report-caption">Acknowledges delivery of the report. It does not document transfer of clinical responsibility or replace the verbal handoff.</p>
        </section>
        <section className="report-section secondary-record" aria-label="Supporting record">
          <details><summary>Report activity ({incident.auditEvents.length})</summary><ol className="report-activity">{[...incident.auditEvents].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt)).map(event => <li key={event.id}><strong>{readableLabel(event.action)}</strong><span>{event.details}</span><ReportTime value={event.createdAt} /></li>)}</ol></details>
          <details><summary>Structured export</summary><p className="report-caption">FHIR-inspired JSON for inspecting the structured report.</p><button type="button" className="ghost" disabled={pending || !!action} onClick={() => run('Generating export…', onExport)}>Generate FHIR-like JSON</button>{exportJson && <pre className="export" tabIndex={0} aria-label="Exported report JSON">{exportJson}</pre>}</details>
        </section>
      </aside>
    </div>
  </section>;
}

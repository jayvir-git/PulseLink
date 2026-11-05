import { NARRATIVE_LIMIT } from '../api/limits';
import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';

export function NewIncidentPage() {
  const navigate = useNavigate();
  const [hospitals, setHospitals] = useState<{ id: string; name: string; city: string }[]>([]);
  const [chiefComplaint, setChiefComplaint] = useState('');
  const [patientAgeRange, setPatientAgeRange] = useState('');
  const [patientSex, setPatientSex] = useState('Unknown');
  const [destinationHospitalId, setDestinationHospitalId] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [lookupError, setLookupError] = useState('');
  const [lookupAttempt, setLookupAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    setLookupError('');
    api.hospitals().then((list) => {
      if (active) setHospitals(list);
    }).catch(err => { if (active) setLookupError(err instanceof Error ? err.message : 'Could not load hospitals'); });
    return () => { active = false; };
  }, [lookupAttempt]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (loading || notes.length > NARRATIVE_LIMIT) return;
    setLoading(true);
    setError('');
    try {
      const created = await api.createIncident({
        chiefComplaint,
        patientAgeRange,
        patientSex,
        notes,
        destinationHospitalId: destinationHospitalId || undefined,
      });
      navigate(`/incidents/${created.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Create failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="page narrow receiving-report field-report">
      <div className="report-toolbar"><Link to="/">← Back to list</Link></div>
      <header className="report-heading"><p className="report-kicker">FIELD CARE RECORD</p><h1>Start an incident</h1><p className="report-caption">Start with the presenting complaint. Add observations, treatments and care notes as the incident develops.</p></header>
      <form className="stack report-section" onSubmit={onSubmit}>
        <p className="report-caption">Chief complaint is required. Other details can be left unrecorded.</p>
        <fieldset className="stack new-incident-fields" disabled={loading}>
        <label>
          Chief complaint
          <input
            autoFocus
            value={chiefComplaint}
            onChange={(e) => setChiefComplaint(e.target.value)}
            required
            placeholder="e.g. Chest pain, onset 20 minutes"
          />
        </label>
        <div className="grid-2">
          <label>
            Age range
            <input value={patientAgeRange} onChange={(e) => setPatientAgeRange(e.target.value)} />
          </label>
          <label>
            Sex
            <select aria-label="Sex" value={patientSex} onChange={(e) => setPatientSex(e.target.value)}>
              <option>Unknown</option>
              <option>Female</option>
              <option>Male</option>
              <option>Other</option>
            </select>
          </label>
        </div>
        <label>
          Destination hospital
          <select aria-label="Destination hospital"
            value={destinationHospitalId}
            onChange={(e) => setDestinationHospitalId(e.target.value)}
          >
            <option value="">Choose later — required before transport</option>
            {hospitals.map((h) => (
              <option key={h.id} value={h.id}>
                {h.name} ({h.city})
              </option>
            ))}
          </select>
        </label>
        <label>
          Initial care notes (optional)
<textarea aria-label="Initial care notes (optional)" aria-describedby="initial-notes-limit" aria-invalid={notes.length > NARRATIVE_LIMIT} value={notes} onChange={(e) => setNotes(e.target.value)} rows={4} />
        </label>
        <p id="initial-notes-limit" className={notes.length > NARRATIVE_LIMIT ? "error" : "form-hint"}>{notes.length} / {NARRATIVE_LIMIT} characters.{notes.length > NARRATIVE_LIMIT && " Shorten the narrative before saving; your text has been kept."}</p>
        {lookupError && <p className="error" role="alert">Destinations could not be loaded. You can still create a draft and choose one later. <button className="ghost" type="button" onClick={() => setLookupAttempt(value => value + 1)}>Retry hospitals</button></p>}
        {error && <p className="error" role="alert">{error}</p>}
        <button type="submit" disabled={loading || notes.length > NARRATIVE_LIMIT}>
          {loading ? 'Creating…' : 'Create draft'}
        </button>
        </fieldset>
      </form>
    </section>
  );
}

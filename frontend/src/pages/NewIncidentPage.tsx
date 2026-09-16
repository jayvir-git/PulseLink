import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
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

  useEffect(() => {
    api.hospitals().then((list) => {
      setHospitals(list);
      if (list[0]) setDestinationHospitalId(list[0].id);
    }).catch(err => setError(err instanceof Error ? err.message : 'Could not load hospitals'));
  }, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
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
    <section className="page narrow">
      <p className="eyebrow">Paramedic</p>
      <h1>New PCR incident</h1>
      <form className="stack card-form" onSubmit={onSubmit}>
        <label>
          Chief complaint
          <input
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
            <select value={patientSex} onChange={(e) => setPatientSex(e.target.value)}>
              <option>Unknown</option>
              <option>Female</option>
              <option>Male</option>
              <option>Other</option>
            </select>
          </label>
        </div>
        <label>
          Destination hospital
          <select
            value={destinationHospitalId}
            onChange={(e) => setDestinationHospitalId(e.target.value)}
          >
            {hospitals.map((h) => (
              <option key={h.id} value={h.id}>
                {h.name} ({h.city})
              </option>
            ))}
          </select>
        </label>
        <label>
          Notes
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={4} />
        </label>
        {error && <p className="error">{error}</p>}
        <button type="submit" disabled={loading}>
          {loading ? 'Creating…' : 'Create draft'}
        </button>
      </form>
    </section>
  );
}

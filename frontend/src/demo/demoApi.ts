import { ApiError } from '../api/errors';
import type {
  AuthUser, CreateIncidentBody, IncidentDetail, InterventionOperation, PagedIncidentList,
} from '../api/client';
import {
  AGENCIES, AGENCY, DEMO_USERS, HOSPITALS, PARAMEDIC_USER_ID,
} from './fixture';
import { canTransition, nextStatuses, requiresDestination } from './statusMachine';
import { loadState, newVersion, saveState, type DemoState } from './store';

const AUTH_KEY = 'pulselink.auth';
const REPLAY_WINDOW_HOURS = 24;

// Same shapes the API returns, so the UI's error handling is exercised rather
// than bypassed. Codes and statuses are taken from IncidentsController.
const conflict = () => new ApiError(412, 'This incident changed since you loaded it. Reload and review the latest state before submitting again.', { code: 'incident_conflict' });
const versionRequired = () => new ApiError(428, 'Reload the incident and send its version in If-Match.', { code: 'version_required' });
const invalidVersion = () => new ApiError(400, 'If-Match must contain one quoted incident version.', { code: 'invalid_version' });
const forbidden = () => new ApiError(403, 'This account cannot change incidents.', null);
const notFound = () => new ApiError(404, 'Incident not found.', null);

function currentUser(): AuthUser {
  const raw = localStorage.getItem(AUTH_KEY);
  if (!raw) throw new ApiError(401, 'Sign in to continue.', null);
  return JSON.parse(raw) as AuthUser;
}

function userId(user: AuthUser): string {
  return user.role === 'Paramedic' ? PARAMEDIC_USER_ID : `demo-user-${user.role.toLowerCase()}`;
}

function canWrite(user: AuthUser): boolean {
  return user.role === 'Paramedic' || user.role === 'Admin';
}

// Mirrors IncidentRoleQueries: hospital staff see a destination match only from
// transport onwards; a paramedic sees their agency's work plus their own.
function visibleTo(user: AuthUser, incident: IncidentDetail): boolean {
  if (user.role === 'Admin') return true;
  if (user.role === 'HospitalStaff') {
    return !!user.hospitalId
      && incident.destinationHospitalId === user.hospitalId
      && requiresDestination(incident.status);
  }
  return incident.agencyId === user.agencyId || incident.createdByUserId === userId(user);
}

function withComputed(incident: IncidentDetail): IncidentDetail {
  return { ...incident, allowedNextStatuses: nextStatuses(incident.status) };
}

function checkVersion(incident: IncidentDetail, version: string): void {
  if (!version) throw versionRequired();
  if (version.length !== 36) throw invalidVersion();
  if (incident.version !== version) throw conflict();
}

function mutate(id: string, apply: (incident: IncidentDetail, state: DemoState, user: AuthUser) => void): IncidentDetail {
  const state = loadState();
  const user = currentUser();
  const incident = state.incidents.find((i) => i.id === id);
  if (!incident) throw notFound();
  if (!visibleTo(user, incident)) throw forbidden();
  if (!canWrite(user)) throw forbidden();
  apply(incident, state, user);
  incident.version = newVersion();
  incident.updatedAt = new Date().toISOString();
  saveState(state);
  return withComputed(incident);
}

function audit(incident: IncidentDetail, user: AuthUser, action: string, details: string): void {
  incident.auditEvents.push({
    id: `demo-audit-${crypto.randomUUID()}`,
    actorUserId: userId(user),
    action,
    details,
    createdAt: new Date().toISOString(),
  });
}

function fingerprintOf(body: Record<string, unknown>): string {
  return JSON.stringify({
    name: body.name ?? '', medication: body.medication ?? '', dose: body.dose ?? '',
    route: body.route ?? '', notes: body.notes ?? '',
  });
}

function hospitalName(id: string | null | undefined): string | null {
  return HOSPITALS.find((h) => h.id === id)?.name ?? null;
}

export const demoApi = {
  async login(email: string, password: string): Promise<AuthUser> {
    const user = DEMO_USERS[email.trim().toLowerCase()];
    if (!user || !password) throw new ApiError(401, 'Invalid email or password.', null);
    return { ...user };
  },

  async hospitals() {
    return HOSPITALS.map((h) => ({ ...h }));
  },

  async agencies() {
    return AGENCIES.map((a) => ({ ...a }));
  },

  async incidents(page = 1): Promise<PagedIncidentList> {
    const user = currentUser();
    const pageSize = 50;
    const visible = loadState().incidents
      .filter((incident) => visibleTo(user, incident))
      .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
    return {
      items: visible.slice((page - 1) * pageSize, page * pageSize).map(withComputed),
      page,
      pageSize,
      totalCount: visible.length,
    };
  },

  async incident(id: string): Promise<IncidentDetail> {
    const user = currentUser();
    const incident = loadState().incidents.find((i) => i.id === id);
    if (!incident) throw notFound();
    if (!visibleTo(user, incident)) throw forbidden();
    return withComputed(incident);
  },

  async createIncident(body: CreateIncidentBody): Promise<IncidentDetail> {
    const state = loadState();
    const user = currentUser();
    if (!canWrite(user)) throw forbidden();
    const now = new Date().toISOString();
    const incident: IncidentDetail = {
      id: `demo-incident-${crypto.randomUUID()}`,
      incidentNumber: `INC-2026-${String(state.incidents.length + 1).padStart(4, '0')}`,
      status: 'Draft',
      chiefComplaint: body.chiefComplaint,
      agencyName: AGENCY.name,
      destinationHospitalName: hospitalName(body.destinationHospitalId),
      createdAt: now,
      updatedAt: now,
      version: newVersion(),
      patientAgeRange: body.patientAgeRange ?? null,
      patientSex: body.patientSex ?? null,
      notes: body.notes ?? null,
      agencyId: AGENCY.id,
      destinationHospitalId: body.destinationHospitalId ?? null,
      createdByUserId: userId(user),
      handedOffAt: null,
      allowedNextStatuses: [],
      vitalSigns: [],
      interventions: [],
      auditEvents: [],
    };
    audit(incident, user, 'IncidentCreated', `Incident ${incident.incidentNumber} created`);
    state.incidents.push(incident);
    saveState(state);
    return withComputed(incident);
  },

  async updateIncident(id: string, body: CreateIncidentBody, version: string): Promise<IncidentDetail> {
    return mutate(id, (incident, _state, user) => {
      checkVersion(incident, version);
      if (incident.status === 'HandedOff') throw new ApiError(400, 'Cannot edit after handoff.', null);
      if (requiresDestination(incident.status) && !body.destinationHospitalId) {
        throw new ApiError(400, 'A destination hospital is required before transport or handoff.', null);
      }
      incident.chiefComplaint = body.chiefComplaint;
      incident.patientAgeRange = body.patientAgeRange ?? null;
      incident.patientSex = body.patientSex ?? null;
      incident.notes = body.notes ?? null;
      incident.destinationHospitalId = body.destinationHospitalId ?? null;
      incident.destinationHospitalName = hospitalName(body.destinationHospitalId);
      audit(incident, user, 'IncidentUpdated', 'Incident details updated');
    });
  },

  async addVital(id: string, body: Record<string, unknown>, version: string): Promise<IncidentDetail> {
    return mutate(id, (incident, _state, user) => {
      checkVersion(incident, version);
      if (incident.status === 'HandedOff') throw new ApiError(400, 'Cannot add vitals after handoff.', null);
      const num = (key: string) => (body[key] === '' || body[key] === undefined || body[key] === null
        ? null : Number(body[key]));
      incident.vitalSigns.push({
        id: `demo-vital-${crypto.randomUUID()}`,
        recordedAt: new Date().toISOString(),
        heartRate: num('heartRate'),
        systolicBp: num('systolicBp'),
        diastolicBp: num('diastolicBp'),
        respiratoryRate: num('respiratoryRate'),
        spO2: num('spO2'),
        temperatureC: num('temperatureC'),
        glasgowComaScale: (body.glasgowComaScale as string) || null,
      });
      audit(incident, user, 'VitalRecorded', 'Vital signs recorded');
    });
  },

  async addIntervention(
    id: string, body: Record<string, unknown>, version: string, key: string,
  ): Promise<InterventionOperation> {
    const state = loadState();
    const user = currentUser();
    const incident = state.incidents.find((i) => i.id === id);
    if (!incident) throw notFound();
    if (!visibleTo(user, incident)) throw forbidden();
    if (!canWrite(user)) throw forbidden();
    if (!key) throw new ApiError(400, 'Send a nonempty UUID in Idempotency-Key for this intervention.', { code: 'idempotency_key_required' });

    const fingerprint = fingerprintOf(body);
    const actor = userId(user);
    // The replay lookup runs before the version check, exactly as the API does:
    // a retry has to succeed even though the first attempt moved the version on.
    const prior = state.replays.find((r) => r.incidentId === id && r.actorUserId === actor && r.key === key);
    if (prior) {
      if (new Date(prior.expiresAtUtc) <= new Date()) {
        throw new ApiError(410, 'This completed request has expired. Review the incident; do not resend it as a new intervention.', { code: 'idempotency_key_expired' });
      }
      if (prior.fingerprint !== fingerprint) {
        throw new ApiError(409, 'This request key was already used with different intervention details.', { code: 'idempotency_key_reused' });
      }
      return { incidentId: id, interventionId: prior.interventionId, performedAt: prior.performedAt };
    }

    checkVersion(incident, version);
    if (incident.status === 'HandedOff') throw new ApiError(400, 'Cannot add interventions after handoff.', null);

    const performedAt = new Date().toISOString();
    const interventionId = `demo-intervention-${crypto.randomUUID()}`;
    incident.interventions.push({
      id: interventionId,
      performedAt,
      name: String(body.name ?? '').trim(),
      medication: (body.medication as string) || null,
      dose: (body.dose as string) || null,
      route: (body.route as string) || null,
      notes: (body.notes as string) || null,
    });
    audit(incident, user, 'InterventionAdded', `Added intervention: ${String(body.name ?? '')}`);
    incident.version = newVersion();
    incident.updatedAt = performedAt;
    state.replays.push({
      incidentId: id,
      actorUserId: actor,
      key,
      fingerprint,
      interventionId,
      performedAt,
      expiresAtUtc: new Date(Date.now() + REPLAY_WINDOW_HOURS * 3_600_000).toISOString(),
    });
    saveState(state);
    return { incidentId: id, interventionId, performedAt };
  },

  async transition(id: string, toStatus: string, version: string): Promise<IncidentDetail> {
    return mutate(id, (incident, _state, user) => {
      checkVersion(incident, version);
      if (!canTransition(incident.status, toStatus)) {
        throw new ApiError(400, `Cannot transition from ${incident.status} to ${toStatus}.`, null);
      }
      if (requiresDestination(toStatus) && !incident.destinationHospitalId) {
        throw new ApiError(400, 'A destination hospital is required before transport or handoff.', null);
      }
      incident.status = toStatus;
      if (toStatus === 'HandedOff') incident.handedOffAt = new Date().toISOString();
      audit(incident, user, 'StatusChanged', `Status changed to ${toStatus}`);
    });
  },

  async exportHandoff(id: string): Promise<unknown> {
    const incident = await demoApi.incident(id);
    // Shape follows the API's FHIR-inspired bundle closely enough to read; the
    // server remains the authority for the real export.
    return {
      resourceType: 'Bundle',
      type: 'document',
      timestamp: new Date().toISOString(),
      entry: [
        {
          resource: {
            resourceType: 'Encounter',
            identifier: [{ value: incident.incidentNumber }],
            status: incident.status,
            code: { text: incident.chiefComplaint },
            serviceProvider: { display: incident.agencyName },
            destination: { display: incident.destinationHospitalName },
          },
        },
        ...incident.vitalSigns.map((vital) => ({
          resource: {
            resourceType: 'Observation', status: 'final', effectiveDateTime: vital.recordedAt,
            component: [
              { code: { text: 'Heart rate' }, valueQuantity: { value: vital.heartRate, unit: 'beats/min' } },
              { code: { text: 'SpO2' }, valueQuantity: { value: vital.spO2, unit: '%' } },
            ],
          },
        })),
        ...incident.interventions.map((intervention) => ({
          resource: {
            resourceType: 'Procedure', status: 'completed',
            performedDateTime: intervention.performedAt, code: { text: intervention.name },
          },
        })),
      ],
    };
  },
};

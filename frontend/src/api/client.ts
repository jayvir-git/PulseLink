const API_BASE = import.meta.env.VITE_API_URL ?? '';

export class ApiError extends Error {
  readonly status: number;
  readonly details: unknown;

  constructor(status: number, message: string, details: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.details = details;
  }

  get code(): string | undefined {
    return this.details !== null && typeof this.details === 'object' && 'code' in this.details
      && typeof this.details.code === 'string' ? this.details.code : undefined;
  }
}

function versionHeader(version: string): HeadersInit {
  return { 'If-Match': `"${version}"` };
}

export type AuthUser = {
  token: string;
  email: string;
  displayName: string;
  role: string;
  agencyId?: string | null;
  hospitalId?: string | null;
};

function authHeaders(): HeadersInit {
  const raw = localStorage.getItem('pulselink.auth');
  if (!raw) return { 'Content-Type': 'application/json' };
  const auth = JSON.parse(raw) as AuthUser;
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${auth.token}`,
  };
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      ...authHeaders(),
      ...(init?.headers ?? {}),
    },
  });

  if (!res.ok) {
    let message = `Request failed (${res.status})`;
    let details: unknown = null;
    try {
      details = await res.json();
      if (details !== null && typeof details === 'object' && 'message' in details
        && typeof details.message === 'string') message = details.message;
    } catch {
      /* ignore */
    }
    throw new ApiError(res.status, message, details);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const api = {
  login: (email: string, password: string) =>
    request<AuthUser>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }).then((r) => ({
      token: r.token,
      email: r.email,
      displayName: r.displayName,
      role: r.role,
      agencyId: r.agencyId,
      hospitalId: r.hospitalId,
    })),

  hospitals: () =>
    request<{ id: string; name: string; city: string }[]>('/api/lookup/hospitals'),

  agencies: () =>
    request<{ id: string; name: string; region: string }[]>('/api/lookup/agencies'),

  incidents: (page = 1) =>
    request<PagedIncidentList>(`/api/incidents?page=${page}&pageSize=50`),

  incident: (id: string) => request<IncidentDetail>(`/api/incidents/${id}`),

  createIncident: (body: CreateIncidentBody) =>
    request<IncidentDetail>('/api/incidents', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  updateIncident: (id: string, body: CreateIncidentBody, version: string) =>
    request<IncidentDetail>(`/api/incidents/${id}`, {
      method: 'PUT',
      headers: versionHeader(version),
      body: JSON.stringify(body),
    }),

  addVital: (id: string, body: Record<string, unknown>, version: string) =>
    request<IncidentDetail>(`/api/incidents/${id}/vitals`, {
      method: 'POST',
      headers: versionHeader(version),
      body: JSON.stringify(body),
    }),

  addIntervention: (id: string, body: Record<string, unknown>, version: string, key: string) =>
    request<InterventionOperation>(`/api/incidents/${id}/interventions`, {
      method: 'POST',
      headers: { ...versionHeader(version), 'Idempotency-Key': key },
      body: JSON.stringify(body),
    }),

  transition: (id: string, toStatus: string, version: string) =>
    request<IncidentDetail>(`/api/incidents/${id}/status`, {
      method: 'POST',
      headers: versionHeader(version),
      body: JSON.stringify({ toStatus }),
    }),

  exportHandoff: (id: string) => request<unknown>(`/api/incidents/${id}/export`),
};

export type PagedIncidentList = {
  items: IncidentSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type InterventionOperation = {
  incidentId: string;
  interventionId: string;
  performedAt: string;
};

export type IncidentSummary = {
  id: string;
  incidentNumber: string;
  status: string;
  chiefComplaint: string;
  agencyName: string;
  destinationHospitalName?: string | null;
  createdAt: string;
  updatedAt: string;
};

export type IncidentDetail = IncidentSummary & {
  version: string;
  patientAgeRange?: string | null;
  patientSex?: string | null;
  notes?: string | null;
  agencyId: string;
  destinationHospitalId?: string | null;
  createdByUserId: string;
  handedOffAt?: string | null;
  allowedNextStatuses: string[];
  vitalSigns: {
    id: string;
    recordedAt: string;
    heartRate?: number | null;
    systolicBp?: number | null;
    diastolicBp?: number | null;
    respiratoryRate?: number | null;
    spO2?: number | null;
    temperatureC?: number | null;
    glasgowComaScale?: string | null;
  }[];
  interventions: {
    id: string;
    performedAt: string;
    name: string;
    medication?: string | null;
    dose?: string | null;
    route?: string | null;
    notes?: string | null;
  }[];
  auditEvents: {
    id: string;
    actorUserId: string;
    action: string;
    details: string;
    createdAt: string;
  }[];
};

export type CreateIncidentBody = {
  chiefComplaint: string;
  patientAgeRange?: string;
  patientSex?: string;
  notes?: string;
  destinationHospitalId?: string;
};

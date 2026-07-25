const API_BASE = import.meta.env.VITE_API_URL ?? '';

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
    try {
      const body = await res.json();
      message = body.message ?? message;
    } catch {
      /* ignore */
    }
    throw new Error(message);
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

  incidents: () => request<IncidentSummary[]>('/api/incidents'),

  incident: (id: string) => request<IncidentDetail>(`/api/incidents/${id}`),

  createIncident: (body: CreateIncidentBody) =>
    request<IncidentDetail>('/api/incidents', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  updateIncident: (id: string, body: CreateIncidentBody) =>
    request<IncidentDetail>(`/api/incidents/${id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),

  addVital: (id: string, body: Record<string, unknown>) =>
    request<IncidentDetail>(`/api/incidents/${id}/vitals`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  addIntervention: (id: string, body: Record<string, unknown>) =>
    request<IncidentDetail>(`/api/incidents/${id}/interventions`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  transition: (id: string, toStatus: string) =>
    request<IncidentDetail>(`/api/incidents/${id}/status`, {
      method: 'POST',
      body: JSON.stringify({ toStatus }),
    }),

  exportHandoff: (id: string) => request<unknown>(`/api/incidents/${id}/export`),
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

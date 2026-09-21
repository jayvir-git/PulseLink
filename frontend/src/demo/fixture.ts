import type { AuthUser, IncidentDetail } from '../api/client';

// Same organisations and accounts the real seeder creates, so a reader moving
// between the demo and the repository sees the same names.
export const AGENCY = { id: '11111111-1111-1111-1111-111111111111', name: 'Camden County EMS', region: 'NJ-South' };
export const COOPER = { id: '22222222-2222-2222-2222-222222222222', name: 'Cooper University Hospital', city: 'Camden' };
export const VIRTUA = { id: '33333333-3333-3333-3333-333333333333', name: 'Virtua Our Lady of Lourdes', city: 'Camden' };

export const HOSPITALS = [COOPER, VIRTUA];
export const AGENCIES = [AGENCY];

export const PARAMEDIC_USER_ID = 'demo-user-paramedic';

export const DEMO_USERS: Record<string, AuthUser> = {
  'paramedic@pulselink.demo': {
    token: 'demo', email: 'paramedic@pulselink.demo', displayName: 'Paramedic Demo',
    role: 'Paramedic', agencyId: AGENCY.id, hospitalId: null,
  },
  'hospital@pulselink.demo': {
    token: 'demo', email: 'hospital@pulselink.demo', displayName: 'Hospital Demo',
    role: 'HospitalStaff', agencyId: null, hospitalId: COOPER.id,
  },
  'admin@pulselink.demo': {
    token: 'demo', email: 'admin@pulselink.demo', displayName: 'Admin Demo',
    role: 'Admin', agencyId: null, hospitalId: null,
  },
};

function minutesAgo(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

type Seed = {
  number: string;
  status: string;
  chiefComplaint: string;
  destination: typeof COOPER | null;
  ageRange: string;
  sex: string;
  createdMinutesAgo: number;
};

const SEEDS: Seed[] = [
  { number: 'INC-2026-0001', status: 'Transporting', chiefComplaint: 'Chest pain, radiating to left arm', destination: COOPER, ageRange: '60-69', sex: 'Male', createdMinutesAgo: 22 },
  { number: 'INC-2026-0002', status: 'Draft', chiefComplaint: 'Fall from ladder, possible wrist fracture', destination: null, ageRange: '40-49', sex: 'Female', createdMinutesAgo: 6 },
  { number: 'INC-2026-0003', status: 'OnScene', chiefComplaint: 'Shortness of breath', destination: VIRTUA, ageRange: '70-79', sex: 'Female', createdMinutesAgo: 14 },
  { number: 'INC-2026-0004', status: 'Arrived', chiefComplaint: 'Motor vehicle collision, restrained driver', destination: COOPER, ageRange: '30-39', sex: 'Male', createdMinutesAgo: 48 },
  { number: 'INC-2026-0005', status: 'HandedOff', chiefComplaint: 'Syncope, brief loss of consciousness', destination: COOPER, ageRange: '50-59', sex: 'Female', createdMinutesAgo: 95 },
  { number: 'INC-2026-0006', status: 'EnRoute', chiefComplaint: 'Allergic reaction after insect sting', destination: COOPER, ageRange: '20-29', sex: 'Male', createdMinutesAgo: 3 },
];

export function seedIncidents(): IncidentDetail[] {
  return SEEDS.map((seed, index) => {
    const created = minutesAgo(seed.createdMinutesAgo);
    const detail: IncidentDetail = {
      id: `demo-incident-${index + 1}`,
      incidentNumber: seed.number,
      status: seed.status,
      chiefComplaint: seed.chiefComplaint,
      agencyName: AGENCY.name,
      destinationHospitalName: seed.destination?.name ?? null,
      createdAt: created,
      updatedAt: created,
      version: '1',
      patientAgeRange: seed.ageRange,
      patientSex: seed.sex,
      notes: null,
      agencyId: AGENCY.id,
      destinationHospitalId: seed.destination?.id ?? null,
      createdByUserId: PARAMEDIC_USER_ID,
      handedOffAt: seed.status === 'HandedOff' ? minutesAgo(seed.createdMinutesAgo - 30) : null,
      allowedNextStatuses: [],
      vitalSigns: [],
      interventions: [],
      auditEvents: [{
        id: `demo-audit-${index + 1}-created`,
        actorUserId: PARAMEDIC_USER_ID,
        action: 'IncidentCreated',
        details: `Incident ${seed.number} created`,
        createdAt: created,
      }],
    };

    if (seed.status !== 'Draft') {
      detail.vitalSigns.push({
        id: `demo-vital-${index + 1}`,
        recordedAt: minutesAgo(seed.createdMinutesAgo - 2),
        heartRate: 88 + index,
        systolicBp: 128 + index,
        diastolicBp: 78,
        respiratoryRate: 18,
        spO2: 96,
        temperatureC: 36.8,
        glasgowComaScale: '15',
      });
    }

    if (seed.status === 'Transporting' || seed.status === 'Arrived' || seed.status === 'HandedOff') {
      detail.interventions.push({
        id: `demo-intervention-${index + 1}`,
        performedAt: minutesAgo(seed.createdMinutesAgo - 4),
        name: 'IV access established',
        medication: null,
        dose: null,
        route: 'Left antecubital',
        notes: null,
      });
    }

    return detail;
  });
}

import type { IncidentDetail } from '../api/client';
import { seedIncidents } from './fixture';

const STORAGE_KEY = 'pulselink.demo.state';
const SCHEMA = 1;

export type ReplayRecord = {
  incidentId: string;
  actorUserId: string;
  key: string;
  fingerprint: string;
  interventionId: string;
  performedAt: string;
  expiresAtUtc: string;
};

export type DemoState = {
  schema: number;
  incidents: IncidentDetail[];
  replays: ReplayRecord[];
};

export function newVersion(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  // Deterministic fallback keeps the 36-character shape the real If-Match check requires.
  const hex = (n: number) => Math.floor(Math.random() * 16 ** n).toString(16).padStart(n, '0');
  return `${hex(8)}-${hex(4)}-4${hex(3)}-a${hex(3)}-${hex(12)}`;
}

function freshState(): DemoState {
  return {
    schema: SCHEMA,
    incidents: seedIncidents().map((incident) => ({ ...incident, version: newVersion() })),
    replays: [],
  };
}

// Every call re-reads storage rather than holding a module-level copy, so two
// browser tabs see each other's writes. That is what makes the conflict and
// retry behaviour observable without a server.
export function loadState(): DemoState {
  let raw: string | null = null;
  try {
    raw = localStorage.getItem(STORAGE_KEY);
  } catch {
    return freshState();
  }
  if (!raw) {
    const seeded = freshState();
    saveState(seeded);
    return seeded;
  }
  try {
    const parsed = JSON.parse(raw) as DemoState;
    if (parsed.schema !== SCHEMA || !Array.isArray(parsed.incidents)) throw new Error('stale shape');
    return parsed;
  } catch {
    const seeded = freshState();
    saveState(seeded);
    return seeded;
  }
}

export function saveState(state: DemoState): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    /* A demo that cannot persist still works for the life of the page. */
  }
}

export function resetDemo(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* ignore */
  }
  saveState(freshState());
}

// Mirrors backend/PulseLink.Core/Domain/IncidentStatusMachine.cs.
// frontend/tests/demo-status-machine.test.mjs fails the build if the two drift.
export const ALLOWED_TRANSITIONS: Record<string, string[]> = {
  Draft: ['EnRoute'],
  EnRoute: ['OnScene'],
  OnScene: ['Transporting'],
  Transporting: ['Arrived'],
  Arrived: ['HandedOff'],
  HandedOff: [],
};

// Same rule as EnsureValidState: a destination is required from transport onwards,
// and it is checked on edits too, not only on transitions.
export const DESTINATION_REQUIRED_FROM = ['Transporting', 'Arrived', 'HandedOff'];

export function nextStatuses(current: string): string[] {
  return ALLOWED_TRANSITIONS[current] ?? [];
}

export function canTransition(from: string, to: string): boolean {
  return nextStatuses(from).includes(to);
}

export function requiresDestination(status: string): boolean {
  return DESTINATION_REQUIRED_FROM.includes(status);
}

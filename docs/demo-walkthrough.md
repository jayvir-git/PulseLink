# PulseLink local demo walkthrough

Password for all seeded users: `Demo123!`

## 1) Paramedic path

1. Sign in as `paramedic@pulselink.demo`
2. Click **New incident**
3. Chief complaint: `Chest pain, onset 20 minutes`
4. Keep destination hospital selected → **Create draft**
5. Add vitals and an intervention (Aspirin)
6. Advance status: EnRoute → OnScene → Transporting → Arrived → HandedOff
7. Click **Generate FHIR-like JSON** and inspect the export payload and audit trail

## 2) Hospital view

1. Sign out, sign in as `hospital@pulselink.demo`
2. Confirm the incident appears in **Incoming handoffs**
3. Open it and review clinical summary, vitals, and interventions

## 3) Admin

1. Sign in as `admin@pulselink.demo`
2. Open **Admin** and review agencies and hospitals
3. Open Incidents and note cross-org visibility

## What this exercises

- Relational PCR model (not a single JSON blob)
- Explicit status machine with validation
- Role-based access by agency/hospital membership
- Append-only audit events
- Handoff export endpoint for receiving systems

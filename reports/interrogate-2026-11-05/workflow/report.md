# Workflow adversarial review

One verified finding. No production changes. The snapshot production frontend built successfully; four existing targeted workflow controls passed. Synthetic API fixtures, isolated headless browser context, ephemeral loopback HTTP server; no user browser or real patient data.

## P2 — Successful treatment retry hides other unsaved clinical entries when the refreshed record has been handed off

**Source:** `frontend/src/pages/IncidentDetailPage.tsx:245–253`, `:358`, `:442` (snapshot and original match). Successful intervention handling clears the intervention attempt and calls `reload()`, but only the failure branch enables `preserveDrafts`. When that successful GET observes `HandedOff`, `canEdit` becomes false and both observation and narrative input sections disappear because their render condition is `canEdit || preserveDrafts`.

**Deterministic trigger:** On an Arrived incident, enter unsaved heart rate `91` and an unsaved EMS narrative, then submit a treatment. The treatment commits but its response is lost. Another paramedic hands off the incident before the original user explicitly retries. The retry correctly reuses the same idempotency key and succeeds; its follow-up detail fetch now returns HandedOff. This is a supported replay sequence, not a stale-write rejection.

**Expected:** Other unsaved clinical values remain visible and disabled, with the existing retained-input warning, so the user can review/recover the unsubmitted observations and narrative. Clinical writes remain closed. Architecture docs lines 98–99 describe visible disabled retention after handoff.

**Actual:** Both draft inputs disappear; the unsaved narrative is absent from the page. The banner still says “Unsaved entries — save each section before leaving this report”, but there are no controls to inspect, save, copy, or clear those entries. The recorded treatment is visible. This is loss of access to in-memory drafts without navigation, not the documented lack of offline/navigation persistence.

**Executable proof:** `snapshot/frontend/tests/workflow-repro.browser.mjs`.

From PowerShell:

```powershell
Set-Location C:/Users/jayvir/Documents/PulseLink/reports/interrogate-2026-11-05/workflow/snapshot/frontend
npm run build
$env:PLAYWRIGHT_BROWSERS_PATH='C:/Users/jayvir/AppData/Local/ms-playwright'
node --test tests/workflow-repro.browser.mjs
```

The browser/server process requires permission for loopback access in this sandbox. The initial unprivileged attempt failed with `ERR_NETWORK_ACCESS_DENIED`; the approved escalated rerun reached the application and failed the behavior assertion below.

Captured output: `snapshot/frontend/workflow-repro-output.txt`:

```text
WORKFLOW_EVIDENCE {"retryRequests":2,"sameKey":true,"savedTreatmentVisible":1,"unsavedStateBanner":"Unsaved entries — save each section before leaving this report","heartRateInputs":0,"narrativeInputs":0,"unsavedNarrativeVisible":0,"retainedInputsWarning":0}
AssertionError [ERR_ASSERTION]: Retained vitals must remain visible after replay refresh observes handoff
0 !== 1
tests 1; pass 0; fail 1
```

## Controls and discarded candidates

Four existing controls pass with this command (output in `snapshot/frontend/workflow-controls-output.txt`):

```powershell
node --test --test-name-pattern='Unsaved clinical|Care notes preserve|Intervention' tests/concurrency.browser.mjs
```

Passed: intervention lost-response replay identity; intervention busy-response identity; explicit unsaved-entry handoff prevention; notes draft retention and reviewed-version retry after conflict.

Four candidate classes dropped:

1. Lost inputs on leaving the page: explicitly documented navigation persistence limitation and shown in the UI; no new defect claimed.
2. Duplicate creation/vitals after lost responses: architecture explicitly excludes these operations from intervention idempotency; no new defect claimed.
3. Local handoff with unsaved clinical values: existing executable control passes; UI prevents that handoff.
4. Notes conflict losing the draft or reusing the stale version: existing executable control passes; draft survives and reviewed version is used.

Context7 was consulted for Playwright route fixtures. Installed Playwright is 1.62.1; Context7 had no exact 1.62.1 entry, so only stable route/fulfill/abort APIs already present in the local harness were reused. Local runtime execution verified those APIs; no version-specific behavior was assumed.

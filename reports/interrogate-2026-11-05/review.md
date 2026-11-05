# PulseLink interrogate review — 2026-11-05

## Intent and scope

A paramedic should be able to create an incident, document evolving observations and treatments, select a destination, and complete handoff without silently losing access to unsaved work. The intended hospital should see the appropriate incoming and arrived reports and acknowledge receipt independently of clinical responsibility. The redesigned field, receiving, queue, and header interfaces should remain usable with keyboard navigation and narrow screens.

Reviewed the current working checkout, including uncommitted UI/accessibility changes, rather than only committed HEAD. This was a read-only review, not an implementation pass.

## Method

Applied pstack interrogate's independent adversary and executable-proof process. Its default Claude and Grok models were unavailable (both model requests returned Unknown model), so three independent parent-model reviewers used the skill's permitted fallback. This is not a cross-provider consensus review.

Workflow, backend, and accessibility reviewers worked in separate snapshots with synthetic records and isolated browser/database fixtures. The parent independently reran both retained findings and observed the failures described below. This follows Separate Before Serializing Shared State and Prove It Works. Hash verification after review found **0 changes across 103 application source files**. Review reports and reproduction tests were added only under this report directory.

## Retained findings

### P2 — Successful treatment retry hides other unsaved clinical entries after concurrent handoff

Attribution: workflow adversary; independently reproduced by parent.

On an Arrived incident, enter unsaved vitals and narrative. Submit a treatment whose response is lost after committing. Another clinician hands off the incident; retry the treatment with its existing idempotency key. The replay succeeds, but the refresh observes HandedOff and removes the unsaved vitals and narrative inputs. The banner still says there are unsaved entries.

The drafts remain in memory but cannot be inspected or copied through the interface. Expected behavior is visible, disabled retained inputs and the existing retention warning, with further clinical writes closed. This occurs without navigation and is separate from documented persistence limitations.

Source: `frontend/src/pages/IncidentDetailPage.tsx:245` success/reload path; conditional input sections at lines 358 and 442.

Parent rerun, from `workflow/snapshot/frontend`:

```powershell
$env:PLAYWRIGHT_BROWSERS_PATH='C:/Users/jayvir/AppData/Local/ms-playwright'
node --test tests/workflow-repro.browser.mjs
```

Actual: `retryRequests=2`, `sameKey=true`, `savedTreatmentVisible=1`, `heartRateInputs=0`, `narrativeInputs=0`, `unsavedNarrativeVisible=0`, `retainedInputsWarning=0`. Assertion: `Retained vitals must remain visible after replay refresh observes handoff`; `0 !== 1`. One test failed, exit 1.

Full proof, build instructions, and controls: [workflow report](workflow/report.md).

### P2 — Narrative accepted by the UI cannot be persisted to SQL Server

Attribution: backend adversary; independently reproduced by parent.

The narrative editor and request DTO accept 2,001 characters, but the SQL Server Notes column is nvarchar(2000). Saving throws an unhandled DbUpdateException for truncation; the same request succeeds on SQLite. Retrying does not resolve the mismatch. The draft remains in the editor; no data-loss claim is made.

Expected: successful persistence or controlled field validation, with any limit communicated to the user. Source: `frontend/src/pages/IncidentDetailPage.tsx:443`, `backend/PulseLink.Api/Dtos/IncidentDtos.cs:17`, and `backend/PulseLink.Infrastructure/Data/PulseLinkDbContext.cs:49`. Persistence failure reaches `IncidentsController.cs:607`.

Parent rerun, from `backend/snapshot`:

```powershell
dotnet test backend/PulseLink.Tests/PulseLink.Tests.csproj --no-restore --filter FullyQualifiedName~InterrogateNarrativeTests --logger 'console;verbosity=normal'
```

Actual: `Provider=SQL Server narrative length=2001`; `Unhandled exception: DbUpdateException; String or binary data would be truncated ... column 'Notes'`. SQL Server case failed, SQLite case passed: total 2, passed 1, failed 1, exit 1. TestServer propagates the exception; this is not a claim of observing a deployed HTTP 500 response.

Full proof and fixture instructions: [backend report](backend/report.md).

## Dropped candidates and supporting checks

Thirteen candidate classes were dropped: four workflow, five backend, and four accessibility. Reasons and executable evidence are in the individual reports.

- Workflow: documented navigation/idempotency limitations were excluded; local unsaved-entry handoff protection and notes-conflict recovery passed four targeted controls.
- Backend: no proven role-scoping, receipt-race, or stale-write bypass; unsupported direct demo inputs and unproven timing discrepancies were not retained. The reviewer ran 60 passing HTTP state, visibility, concurrency, and receipt checks.
- Accessibility: destination editing remained keyboard reachable; mobile jump targets and tested focused controls were not hidden by sticky navigation; tested medication text did not cause horizontal overflow; the hospital queue receipt test passed. See [accessibility report](accessibility/report.md).

No additional accessibility defect was proven. This is a bounded Chromium review with external fonts blocked, not a full screen-reader or contrast audit or a declaration of accessibility conformance. Both retained defects were independently rerun by the parent; supporting checks were executed by the attributed reviewers.

## Disposition

No application fixes applied. The invoked [pstack interrogate skill](https://github.com/cursor/plugins/blob/main/pstack/skills/interrogate/SKILL.md) specifies: “Report the findings. Do not apply fixes.”

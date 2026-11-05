# Accessibility adversarial review

No run-proven actionable defects found in this bounded review. No production files were changed.

## Reproduction commands

From `C:/Users/jayvir/Documents/PulseLink/reports/interrogate-2026-11-05/accessibility/snapshot/frontend` in PowerShell:

```powershell
$env:VITE_DEMO='1'
npm run build:demo -- --outDir dist-demo
$env:PULSELINK_BROWSER_PATH='C:\Program Files\Google\Chrome\Application\chrome.exe'
node tests/accessibility-probe.mjs
node --test --test-name-pattern='Hospital queues' tests/demo-navigation.browser.mjs
```

The probe creates its own ephemeral loopback HTTP server, headless browser and isolated context, uses synthetic demo data, blocks external font requests, and closes all of them. It never attaches to the user browser. Local loopback requires execution outside this environment's network-restricted sandbox. Initial sandbox execution failed with ERR_NETWORK_ACCESS_DENIED; the subsequent escalated runs completed.

Evidence: `snapshot/frontend/tests/accessibility-probe-output.txt`. Script: `snapshot/frontend/tests/accessibility-probe.mjs`.

## Executed results and dropped candidates

Four candidate defects were dropped:

1. **Destination editor loses keyboard entry position.** Opening Choose destination replaces the focused button and temporarily makes BODY active. However the next Tab deterministically focuses the Receiving hospital SELECT, so the suspected inaccessible editor was not reproduced. Actual output: `destination next tab { tag: 'SELECT', name: 'Receiving hospital' }`. Examined `frontend/src/pages/IncidentDetailPage.tsx` destination editor.
2. **Sticky field navigation hides jump targets or focused controls.** At 320 CSS pixels, Observations, Treatments and Care notes targets began at y=89.53, 89.73 and 90.23; sticky navigation ended at y=73.28. Transport target began y=223.41. Forty forward Tab presses after the Observations jump yielded zero covered-focus detections. No defect established for these inputs and viewport. Examined `frontend/src/index.css` mobile field report navigation and scroll margins.
3. **Hospital medication detail breaks mobile reflow.** At 320 CSS pixels, seeded hospital report and a synthetic `Methylprednisolonesodiumsuccinate` medication both had document scrollWidth=320; at 390 pixels, `Acetylsalicylic-acid-tablet-extended-release` also stayed within the viewport. No horizontal page overflow established. Examined `frontend/src/pages/HospitalHandoff.tsx` treatment details and corresponding CSS.
4. **Hospital queue receipt state mismatches handoff stage.** Existing browser test passed: `Hospital queues distinguish arrival from acknowledgment and update after receipt`; tests=1, pass=1, fail=0. It exercises awaiting/received after acknowledgment and keeps Arrived separate. This supports that particular synthetic flow, not every concurrent or paginated case.

## Bounds

Focused on recent field and receiving layouts, keyboard access, mobile anchors/reflow and receipt queue semantics. This was Chromium only, with external fonts blocked; no full screen-reader or contrast audit was performed. Source inspection found explicit queue focus and hover colors, but no contrast defect is claimed without a failing measurement. Context7 documentation was consulted for Playwright isolated contexts/evaluation; installed package 1.62.1 was used, and Context7 did not expose that exact version as a pinned documentation ID. No finding is inferred from missing tests or broader accessibility conformance.

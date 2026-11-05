# Human factors in EMS handoffs

## What information is lost or hard to retain?

### Takeaway
Observational studies identify incomplete transmission of complaint, observations and treatment information. A persistent, clearly ordered report is a reasonable design response, but these studies did not test PulseLink or establish a specific screen layout.

### Cited Findings
- Goldberg et al., published January–February 2017 (online July 15, 2016), observed/audio-recorded 90 critical patient handoffs at one urban academic ED. A chief concern was communicated in 78% and a complete vital-sign set in 57%. This is a single-site observational sample of resuscitation-area patients, not a population-wide rate. [Primary study abstract](https://pubmed.ncbi.nlm.nih.gov/27420753/)
- Sumner et al., January–February 2019 (online August 30, 2018), reviewed 68 pediatric resuscitation videos at a Level 1 pediatric trauma center. ED staff interrupted 51% of handoffs; questions requesting already communicated information occurred in 40%. The specific pediatric critical-care setting limits extrapolation to routine adult arrivals. [Primary study abstract](https://pubmed.ncbi.nlm.nih.gov/30118642/)

### Inferences
- Prioritize chief complaint, timestamped observations, and interventions over branding or export tools. Keep information available after the verbal report, in predictable positions.
- Preserve meaningful treatment details: action, medication, dose, route, notes, and performed time when recorded. Do not synthesize missing response-to-treatment claims.
- Explicitly distinguish a missing value from a normal finding; use “Not recorded” rather than an unexplained dash.

### Gaps
- No retrieved study validates a particular typography, color, card density, or screen arrangement for this application.
- PulseLink lacks structured patient identity, history, allergies, home medications and ETA. A redesign cannot make this a complete clinical handoff system by relabeling its limited data.

## How should timeliness and communication affect the screen?

### Takeaway
The report should preserve temporal context and support, rather than replace, direct communication.

### Cited Findings
- AHRQ’s current TeamSTEPPS handoff guidance includes uncertainty, treatment response, recent changes and contingency plans; it emphasizes acknowledgment and an opportunity for questions. It says electronic handoffs may be most effective when reinforced by direct communication. This is general safety guidance, not a UI efficacy trial. Accessed November 4, 2026; the retrieved text does not provide a clear publication date. [AHRQ Handoff tool](https://www.ahrq.gov/teamstepps-program/curriculum/communication/tools/handoff.html)

### Inferences
- Show the latest observation record with its actual recorded time and units, and expose earlier records for comparison. Do not silently backfill individual missing metrics from older records into a seemingly simultaneous set.
- Label times precisely: observation time, intervention time, record last updated, and report receipt are different events. A recently updated report does not prove freshly measured vitals.
- Keep the hospital acknowledgment labeled as receipt of this report. The product’s action does not establish that clinical responsibility was transferred or that all information was understood.
- An informational message can explain that the record supports the verbal handoff; it should not imply the software has performed clinical verification.

### Gaps
- The current fields do not support an authoritative clinical trend assessment or treatment-response inference. Do not create alert thresholds, urgency classes, normal/abnormal coloring, or “stable” labels in this design pass.
- Unknown local workflow, escalation and responsibility-transfer policies require validation with actual EMS and receiving staff.

## What can we honestly compare before and after?

### Takeaway
Compare information retrieval and navigation on the same records. Do not substitute agent aesthetic scores for clinician usability evidence.

### Cited Findings
- Elliott et al., May 2023 (online April 23), conducted an observational implementation study at a Canadian Level 1 trauma center. In 140 recorded handovers, 80 used IMIST-AMBO. Compared with a prior institutional baseline, interruptions and parallel conversations decreased and mean handover duration fell from 2:47 to 1:58. The intervention included education and reminders; it did not isolate a software layout effect and was not a randomized UI trial. [Primary study abstract](https://pubmed.ncbi.nlm.nih.gov/37087711/)

### Inferences
- Use identical incidents in both designs. Ask: What is the presenting complaint? When were the latest observations measured? Which values were not recorded? What treatment was performed and when? Was this report received, by whom, and when?
- Record whether each answer is visible without expansion, how much scrolling or navigation is required, and whether units/timestamps are unambiguous. These are interface observations, not measured clinician task-time improvements.
- Include incomplete and multiple-reading fixtures. A single fully populated seed record will conceal important ambiguity.
- Validate keyboard access, narrow screens, pending acknowledgment, failure/retry, and the read-only hospital boundary separately from visual design.

### Gaps
- No clinician participants were recruited or observed in this task. Any redesigned hierarchy remains a research-informed hypothesis for a later task-based usability session.
- Protocol studies cannot establish that a specific screen is safer, faster or clinically superior. Keep such claims out of the comparison.

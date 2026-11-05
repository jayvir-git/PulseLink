# Structured EMS handoff formats

## What do established formats organize, and where do they apply?

### Takeaway
Use the common content priorities as design references, not a compliance label. No single reviewed format establishes a universal hospital-screen layout or a New Jersey requirement.

### Cited Findings
- AHRQ's SBAR tool defines situation, background, assessment, and recommendation/request. Situation includes communicator/patient context and the current problem; assessment can include observations. It explicitly permits adapting the components and order to a team's needs. This is broad clinical communication guidance, not an EMS-only screen specification. Page reviewed November 2019. — [AHRQ TeamSTEPPS SBAR](https://www.ahrq.gov/teamstepps-program/curriculum/communication/tools/sbar.html)
- Victoria's official ambulance-to-ED protocol lists IMIST-AMBO: identification; mechanism/medical complaint; injuries/information related to complaint; signs; treatment/trends; allergies; medication; background history; other information. It says this structure is used by many Victorian health services and Ambulance Victoria and should be followed regardless of IT system. It also requires concise, consistent communication and an opportunity for questions. The page says updated 12 September 2025 but includes references accessed July 2026; avoid claiming a precise revision date beyond this discrepancy. These are Australian jurisdictional requirements, not U.S. requirements. — [Victoria Health protocol](https://www.health.vic.gov.au/patient-care/protocol-handover-ambulance-patients-emergency-department)
- University Hospital Southampton's adult major-trauma guideline uses ATMIST to support preparation for receiving trauma patients. Its pre-alert form collects age/sex, time of incident/onset, mechanism/medical complaint, injuries/examination findings, first vital signs and significant changes, treatment, ETA and receiving-team preparation. The public PDF amendment log ends May 2022 and directs staff to the intranet for the latest version; use as an illustrative local format, not current universal practice. — [UHS adult major trauma guidelines, section 1.2, printed page 10](https://www.uhs.nhs.uk/Media/SUHTExtranet/WessexTraumaNetwork/UHS-adult-major-trauma-guidelines.pdf)

### Inferences
- A practical common sequence is patient/context and presenting problem, observations, treatment, then relevant background. PulseLink can follow this content logic without displaying mnemonic letters or pretending to complete an SBAR assessment/recommendation.
- Trauma-focused ATMIST should not force all general medical reports into invented mechanism/injury fields. Complaint and recorded notes are the faithful available equivalents.

### Gaps
- Direct RCEMLearning page redirected to maintenance; cached search text was not used as decisive evidence. No universal MIST/ATMIST adoption claim or New Jersey compliance claim is established here.

## What evidence supports structure, and what does it not establish?

### Takeaway
Original research supports consistent handoff content and clinician participation. It does not validate a particular screen design, typography, or software outcome.

### Cited Findings
- Iedema et al. (2012) developed and trialed IMIST-AMBO through video-reflexive ethnography: 137 pre/post handovers involving 291 staff, with 368 staff trained. Participants favored it for both trauma and non-trauma. The abstract reports more consistently ordered information, fewer questions/repetitions, and shorter handovers; improved comprehension/retention was suggested rather than proven. The work combines protocol development, practitioner reflection and training, so it does not isolate the effect of a digital layout. — [Original study abstract, BMJ Quality & Safety, PMID 22626739](https://pubmed.ncbi.nlm.nih.gov/22626739/)

### Inferences
- Treat a new UI as a hypothesis to test with receiving clinicians, not as an evidence-proven handoff intervention.
- Preserve timestamps, prior observations, treatment details and narrative notes rather than summarizing away the underlying record. Put communication content ahead of audit/export controls.

### Gaps
- No study reviewed here compares PulseLink's proposed layout to its current layout. Do not assign clinical efficacy or safety scores from this research.

## How can PulseLink honestly map its limited data?

### Takeaway
The supplied product schema covers only part of these formats. Display absent values honestly and keep report receipt separate from transfer of clinical responsibility.

### Cited Findings
- Victoria's protocol describes handover completion as a clinically agreed event involving information exchange and physical transfer. A software receipt alone does not establish those conditions. — [Victoria Health protocol](https://www.health.vic.gov.au/patient-care/protocol-handover-ambulance-patients-emergency-department)
- Available PulseLink fields in the task: complaint, age range, sex, notes, timestamped vitals/interventions, agency/destination, transport status and receipt audit. This is task-provided product evidence, not an externally sourced clinical standard.

### Inferences
- Top: report reference, chief complaint, compact age/sex, agency/destination and transport stage. The reference is a report identifier, not verified patient identity.
- Primary content: latest recorded observation set with its exact time and units; inspectable previous sets; intervention names, dose/route where recorded, time and notes. Do not combine each measure's last nonempty value into an apparently simultaneous set.
- Missing measurement: 'Not recorded' or a clearly defined dash. Never imply zero, normal, or not performed.
- Allergies, home medications, history, onset and ETA lack dedicated schema fields. Do not say 'None', 'No known allergies', or 'No history'. If surfaced, use a concise limitation such as 'Not collected as structured fields in this report' and keep any relevant narrative visible. Do not silently infer these fields from notes.
- Existing intervention medication data describe recorded interventions, not a reconciled home-medication list. A list with no entries means 'No interventions recorded', not proof that no care was delivered.
- Keep acknowledgment labeled report receipt with original staff/time; do not call it acceptance of care, triage completion, or clinical clearance.

### Gaps
- Current schema has no treatment-response field and cannot establish whether an observed change was caused by an intervention. Trends must remain factual, with no automated clinical interpretation.

# US EMS field documentation workflow

## What sequence should the interface support?

### Takeaway
Care documentation develops through repeated observations, treatments and reassessment, rather than one uninterrupted form. A short incident setup followed by easy repeat entries is a defensible design inference, not a validated clinical workflow.

### Cited Findings
- Lincoln Fire & Rescue's BLS protocol, last edited June 27, 2024, places reassessment after secondary assessment: repeat the primary assessment, record vitals, reassess the complaint, assess interventions and response, and maintain or change management. Its documentation guidance requires verbal handoff and available supporting documents to the accepting provider. This is a local example, not national policy; full PDF fetch failed, but indexed source text exposed this section. [Lincoln protocol](https://www.lincoln.ne.gov/files/sharedassets/public/v/6/fire/ems/bls-protocol-june-24-final.pdf)
- The New York CFR BLS protocol's indexed documentation section says patient care and hospital transport must not be delayed for the written report. This concerns CFR-to-arriving-EMS transfer; avoid generalizing its exact requirements to all paramedics. Full fetch returned403. [New York protocol](https://www.health.ny.gov/professionals/ems/pdf/cfrprotocol.pdf)

### Inferences
- Put record-vitals and record-treatment actions before historical lists. Keep prior entries visible nearby or in accessible history, but do not force scrolling through the whole record to document reassessment.
- Keep narrative editable as the encounter develops. Avoid a linear mandatory wizard that treats each section as permanently complete.
- Keep transport progression distinct from clinical recording. Arrival is not proof that clinical work is finished.

### Gaps
- No observation of PulseLink's intended agency, devices, crews or actual shifts was performed. Local workflow and protocols require clinician review.
- This research does not justify a universal reassessment timer; frequency varies by protocol and condition.

## What information and time semantics matter?

### Takeaway
Repeated observations and treatments need their own time context and treatment response. A server save time is not evidence of when a measurement or intervention occurred.

### Cited Findings
- NEMSIS v3.5.0, build251001CP6 dated October1,2025, defines repeatable vital, medication and procedure groups. eVitals.01 refers to when vitals were taken; eMedications.01 to administration; eProcedures.01 to performance. The medication response field distinguishes improved, unchanged and worse, with Not Recorded separate; procedures also have response, success and complication fields. eNarrative.01 is a narrative field rather than a prescribed narrative format. These are data definitions, not a UI specification. [NEMSIS dictionary, printed pages322–326,408–419,427–437](https://nemsis.org/media/nemsis_v3/release-3.5.0/DataDictionary/PDFHTML/EMSDEMSTATE/NEMSISDataDictionary.pdf)
- Michigan's March2026 initial education objectives use epinephrine as a concrete example: documentation includes indication, medication, concentration, dose, route, site, administration time, response and adverse effects, then communication of treatment and response to receiving personnel. [Michigan education objectives, PDF pages16–17](https://www.michigan.gov/mdhhs/-/media/Project/Websites/mdhhs/Inside-MDHHS/Policy-and-Planning/EMS/New-Uploads/23-5-30/260421-Requirements-and-Objectives-Document-42026.pdf?hash=564B1097CF721BDE3DA98BEE44E611ED&rev=226a3d983111485487b47ec211f214ac)

### Inferences
- With the existing PulseLink schema, label timestamps as recorded/saved time and explain that earlier clinical event times belong in notes until true occurrence-time fields exist. Do not relabel server timestamps as measurement times.
- Prompt intervention notes for response, reassessment, complications and earlier event time where applicable; keep them visible on both role screens. This is a transitional free-text solution, not equivalent to structured NEMSIS support.
- Clear a form only after a confirmed save; show saved history and keep failed drafts separate. Do not prepopulate repeat observations from previous values or infer missing values as normal.
- Do not auto-select a treatment response such as unchanged or no complications. Unknown and unrecorded are different from assessed negative findings.

### Gaps
- PulseLink currently lacks several clinically relevant structured fields; this UI redesign alone cannot make it a comprehensive ePCR or establish NEMSIS conformance.

## How should narrative and handoff review fit?

### Takeaway
Narrative should explain the course of the encounter and context not conveyed by structured entries. A final review can expose what was actually saved without claiming completeness or clinical readiness.

### Cited Findings
- Milwaukee County's2017 narrative policy (included in a2019 manual) recommends factual chronological documentation, consistency between selected fields and narrative, treatment response, reassessment, complications and condition at transfer. It also recommends rereading for accuracy. The indexed text was available but the original PDF returned404; treat this as historical supporting evidence, not current local policy. [Historical Milwaukee manual](https://county.milwaukee.gov/files/county/emergency-management/EMS-/Standards-of-Care/COMPLETESTANDARDSMANUALPDF100119.pdf)

### Inferences
- Offer a plain editable EMS narrative with brief prompts for presenting history, relevant background, assessment, changes during transport and handoff context. Do not inject template assertions or generate a story from missing information.
- Before HandedOff closes editing, expose saved observations, treatments/response, notes and destination, plus a clear statement about the editing lock. Block closure while local drafts remain unsaved, or require explicit disposition of those drafts.
- Reuse hospital screen typography and report sections so clinicians can recognize the same record from either side. Field entry controls should stay task oriented rather than reproducing the receiving team's read-only layout exactly.

### Gaps
- A review checklist cannot certify clinical adequacy, and document receipt is not proof of transfer of care. No source validates the proposed PulseLink screen itself.
- No universally preferred narrative acronym was established; avoid enforcing SOAP, CHART or IMIST as the only acceptable way to document field care.

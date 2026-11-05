# US EMS receiving team workflow

## What information does the receiving team need before arrival and at transfer?

### Takeaway
The sources support a concise overview of the presenting problem and recent clinical observations, followed by treatment and its chronology. A prearrival notification and the bedside transfer are related but different interactions; this UI can support the written information channel without claiming to replace either.

### Cited Findings
- The NJ Department of Health hosted 2022 Interim Clinical Practice Guidelines, section 8.2, describe timely en-route notification. The indexed extract lists unit and level of care/ETA; age, sex and status; chief complaint/current condition; vital signs and consciousness; physical findings; relevant history; and diagnostic/treatment results. This is a primary-source indexed extract, not a verified statement of current NJ protocol: the full 15 MB PDF exceeded the web fetch limit. — [NJ 2022 interim guidelines](https://www.nj.gov/health/ems/documents/2022%20Interim%20Clinical%20Practice%20Guidelines.pdf)
- The joint ACEP/ENA/NAEMSP/NAEMT/NASEMSO position statement recommends that key information be available in written/electronic form at the actual transfer, alongside the verbal report. Its examples include vital signs, treatment interventions, and symptom onset for time-sensitive conditions. Publication: March 6, 2014; NAEMSP lists status August 2013 and links this as its current version. — [Joint policy](https://www.tandfonline.com/doi/full/10.3109/10903127.2014.883001); [NAEMSP source record](https://naemsp.org/position-statement/transfer-of-patient-care-between-ems-providers-and-receiving-facilities/)

### Inferences
- A receiving-team header should put complaint, compact demographics, origin/destination, and transport state together. PulseLink lacks ETA, so show status without guessing arrival time.
- Make the latest recorded observation set immediately readable with measurement time and units. Keep earlier sets available for comparison. Never combine unrelated measurements into a fictitious single current set.
- Show interventions with recorded times, medication/dose/route when supplied, and notes. Do not infer a response to treatment from vitals alone.
- Omit unsupported clinical priority/triage badges. Transport status describes workflow, not acuity.

### Gaps
- PulseLink lacks structured symptom onset, allergies, medical history, test attachments, treatment response, precise identity, and ETA. Notes may contain some information, but the UI cannot manufacture it or claim a complete clinical handoff.
- No current local receiving-hospital SOP was verified. This design is evidence-informed rather than protocol-certified.

## How do verbal handoff, written record, and acknowledgment differ?

### Takeaway
The report is a durable information aid; handoff is an interpersonal process that includes clarification and responsibility. PulseLink's current receipt event records report delivery only and must remain clearly bounded that way.

### Cited Findings
- The joint policy says receivers should be able to clarify the exchanged information, written transfer documentation should become part of the permanent medical record, test results should accompany it, and the complete PCR should follow within a clinically relevant timeframe. Verbal information alone is insufficient for subsequent clinicians who were absent. These are professional recommendations, not UI effectiveness evidence. — [Joint policy, 2014](https://www.tandfonline.com/doi/full/10.3109/10903127.2014.883001)
- AHRQ TeamSTEPPS describes handoff as information plus authority/responsibility; it includes uncertainty, treatment response, recent changes and plans. It emphasizes receiver acknowledgment and an opportunity for questions. Electronic handoffs may work best when reinforced by direct communication. Page created/reviewed May 2023. — [AHRQ Handoff tool](https://www.ahrq.gov/teamstepps-program/curriculum/communication/tools/handoff.html)

### Inferences
- Label the existing action as report receipt, retain receiver identity/time, and explicitly avoid wording such as patient accepted or clinical responsibility transferred.
- Prioritize the clinical record over JSON export and audit bookkeeping. Put export/activity in secondary disclosure, without hiding patient observations or interventions behind it.
- A receipt must not imply that every clinical item has been reviewed, that information is complete, or that verbal clarification occurred.

### Gaps
- PulseLink has no support for questions/read-back, assignment of clinical responsibility, bed allocation, or live EHR import. Do not represent these as completed through a receipt click.

## What can the redesign reasonably claim?

### Takeaway
It can improve information organization and task findability within existing product scope. It cannot claim clinical safety, reduced errors, or optimal workflow solely because it follows a mnemonic or receives favorable agent scores.

### Cited Findings
- NAEMSP's July 27, 2017 educational discussion emphasizes both verbal and written channels and notes that format changes alone do not substitute for coordinated process changes and training across EMS and ED. It discusses variation in handoff quality and receiver needs. This is professional educational commentary summarizing studies and reader discussion, not a trial of PulseLink or of this layout. — [NAEMSP EMS-ED handoff discussion](https://naemsp.org/2017-7-27-the-ems-ed-handoff-a-critical-moment-in-patient-care/)
- AHRQ recommends developing a checklist with the actual team, trying it, and adjusting for completeness without excessive complexity or time burden. — [AHRQ Handoff tool, May 2023](https://www.ahrq.gov/teamstepps-program/curriculum/communication/tools/handoff.html)

### Inferences
- Compare old/new screens on concrete retrieval tasks: identify complaint and source; locate latest HR/BP and timestamp; locate intervention/dose/route/time; distinguish missing observations; find receipt identity/status.
- Report number of interactions and visible placement, not invented time savings or clinical outcome improvements. Validate with EMS and ED professionals before considering real operational use.

### Gaps
- No clinician interviews or usability sessions were performed. No evidence establishes one universal handoff screen or information density as best for every ED.
- The 2024 ACEP interfaces policy surfaced in search, but its HTML required sign-in and PDF could not be opened; it is not used as a decisive source here.

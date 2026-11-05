# EMS documentation integrity and handoff review

## What do timestamps and missing values actually mean?

### Takeaway
Clinical event time must not be confused with the time a record was saved. Blank measurements cannot safely be read as normal findings or negative history.

### Cited Findings
- NEMSIS v3.5.1, build 251001 CP2, defines eVitals.01 as the time measurements were taken. Medication and procedure groups separately carry administration/performance times and patient response. This is a data standard, not proof of a particular interface's effectiveness. — [NEMSIS dictionary, pages 332, 416, 426, 436, 445](https://www.nemsis.org/media/nemsis_v3/release-3.5.1/DataDictionary/PDFHTML/EMSDEMSTATE/NEMSISDataDictionary.pdf)
- NEMSIS distinguishes an applicable field left blank, an inapplicable field, and a field not collected by the agency/state. Pertinent negatives such as refusal, inability to complete, and no known drug allergy carry different meanings. — [NEMSIS dictionary explanation](https://www.nemsis.org/media/nemsis_v3/release-3.5.1/DataDictionary/PDFHTML/EMSDEMSTATE/sample.html)
- PulseLink currently assigns both RecordedAt and PerformedAt using DateTimeOffset.UtcNow at request handling, rather than accepting the clinician's event time. — [Local implementation](../../backend/PulseLink.Api/Controllers/IncidentsController.cs), lines 211 and 280 inspected during this research.

### Inferences
- For the bounded UI redesign, label these timestamps “Recorded” and explain that they represent save time. Do not add an event-time control that the API silently ignores.
- Keep empty inputs blank; display absent saved values as “Not recorded.” Do not prefill the prior vital set into a new observation.
- A future event-time feature needs separate event and audit timestamps across API, database, export and both role views; merely renaming PerformedAt is insufficient.

### Gaps
- PulseLink has no structured missing-reason attributes or dedicated event-time entry. This redesign cannot claim NEMSIS conformance.

## What belongs in narrative and treatment review?

### Takeaway
The narrative adds observed context, response and changes that structured fields cannot carry. It should remain editable during active care and visible alongside saved treatments.

### Cited Findings
- NASEMSO's March 2022 model guideline describes narrative as the place for scene context, clinician observations and patient statements, plus history, treatment response and condition changes not adequately expressed in fixed fields. It discusses narrative, SOAP and CHART formats. — [Model guidelines, printed page 371](https://boards.bsd.dli.mt.gov/_docs/med/National-Model-EMS-Clinical-Guidelines-Version-3.pdf)
- NEMSIS includes explicit patient-response fields for medications and procedures, with improved, unchanged and worse choices. — [NEMSIS dictionary, printed pages 426 and 445](https://www.nemsis.org/media/nemsis_v3/release-3.5.1/DataDictionary/PDFHTML/EMSDEMSTATE/NEMSISDataDictionary.pdf)

### Inferences
- Add a clearly labeled narrative save operation using the existing notes field. Offer short prompts for onset/history, assessment, treatment response and changes without auto-generating clinical assertions.
- Make intervention notes a multiline field, prompt for observed response and relevant timing, and display the saved text in the history and hospital report.
- Explicit “unsaved” versus saved feedback matters more than a percentage-complete meter; a count of filled fields cannot certify clinical completeness.

### Gaps
- No local service's approved narrative convention was provided. Do not force a particular mnemonic or add diagnostic recommendations.

## How should review and report closure work?

### Takeaway
Review supports communication, but a software status or receipt does not by itself establish actual transfer of clinical responsibility.

### Cited Findings
- NASEMSO's model guidance calls for identity, complaint/history, medications/allergies, timed vitals, assessments/interventions and response at transfer. An abbreviated report does not replace the complete record. The guidelines explicitly require local adaptation and are not mandatory universal protocols. — [Model guidelines, printed pages 6 and 13](https://boards.bsd.dli.mt.gov/_docs/med/National-Model-EMS-Clinical-Guidelines-Version-3.pdf)
- San Diego County's transfer workflow distinguishes arrival from transfer: physical relocation, verbal report, and receiving staff signature/assumption of care are separate components. This is a local California example, not a national requirement. — [San Diego County EMS](https://www.sandiegocounty.gov/content/sdc/ems/transfer_of_care/)
- The joint 2014 transfer-of-care statement remains listed by NAEMSP; publisher full text returned HTTP 403 during this research, so its specific wording was not relied on here. — [NAEMSP listing](https://naemsp.org/position-statement/transfer-of-patient-care-between-ems-providers-and-receiving-facilities/)

### Inferences
- Place destination and transport progression in a distinct area; keep care-entry actions easy to reach before closure.
- Before HandedOff, show the saved observations, treatment notes and narrative, plus a warning that editing will close. Prevent closing with pending or unsaved clinical input until the user saves or explicitly discards it.
- Preserve the hospital screen's “report receipt” distinction. Do not turn a checkbox into an assertion that a real verbal handoff or responsibility transfer occurred.
- Keep uncertain submission retries tied to the original request identity; never encourage adding a second medication record to compensate for an unknown save outcome.

### Gaps
- The current model lacks full identity, structured allergies/history/onset and a receiving-clinician transfer signature. These are product gaps, not fields that can be safely inferred from notes.
- None of these sources validates this particular redesigned screen in actual field use. Scenario testing with paramedics remains necessary.

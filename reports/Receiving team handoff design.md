# Design the report around receiving care

PulseLink’s hospital screen should help the receiving team retrieve the presenting problem, recent observations, and treatment record during and after an EMS handoff. Its visual hierarchy should follow those information needs, with report receipt and technical bookkeeping clearly separated. The redesign therefore uses a compact hospital-specific report rather than the paramedic documentation layout with editing controls removed. This is an evidence-informed design hypothesis, not a clinically validated interface. It uses existing PulseLink data and does not introduce triage scoring, inferred treatment response, or unsupported patient details.

## The report supports a conversation

The joint ACEP, ENA, NAEMSP, NAEMT, and NASEMSO transfer-of-care policy recommends contemporaneous written or electronic information alongside verbal communication. It identifies vital signs, interventions, and symptom onset among important information and says the receiver needs an opportunity to clarify it. A persistent report supports clinicians who were not present for the initial exchange; it does not replace that exchange. ([Joint policy, 2014](https://www.tandfonline.com/doi/full/10.3109/10903127.2014.883001), [NAEMSP policy record](https://naemsp.org/position-statement/transfer-of-patient-care-between-ems-providers-and-receiving-facilities/))

AHRQ’s handoff guidance includes uncertainty, recent changes, treatment response, acknowledgment, and questions. Its description of handoff includes responsibility, but **PulseLink’s existing receipt records report delivery only**. The interface must not turn that event into a claim that the patient was accepted, clinical responsibility transferred, or every item reviewed. ([AHRQ handoff tool](https://www.ahrq.gov/teamstepps-program/curriculum/communication/tools/handoff.html))

## Structure makes the available facts easier to find

Established formats share an emphasis on patient/context, presenting problem, observations, treatment, and relevant background. Victoria’s IMIST-AMBO protocol is a useful organizational reference for both medical and trauma complaints, but its jurisdictional requirements do not establish a U.S. or New Jersey requirement. The original IMIST-AMBO research combined practitioner participation, training, and structured communication; it did not test this software layout. ([Victoria Health protocol](https://www.health.vic.gov.au/patient-care/protocol-handover-ambulance-patients-emergency-department), [Iedema et al., 2012](https://pubmed.ncbi.nlm.nih.gov/22626739/))

Information omission is a concrete concern: one observational study of 90 critical-patient EMS handoffs found a chief concern communicated in 78% and a complete vital-sign set in 57%. Those single-site findings motivate making the available record easy to retrieve; they do not prove that a particular layout prevents omissions. ([Goldberg et al., 2017](https://pubmed.ncbi.nlm.nih.gov/27420753/))

| Information need | Existing screen | Redesign decision |
|---|---|---|
| Understand the presenting problem | Complaint is prominent, but large headings and stage pills consume space | Compact complaint heading with report reference, age range, sex, agency, destination, and current transport state |
| Identify recent observations | Measurements share one dense text line | Individually labeled values with units and the observation set’s timestamp |
| Compare prior readings | Repeated text blocks | Expandable chronological table, newest first, preserving every set’s time |
| Understand documented treatment | Name and a combined medication/time line; intervention notes omitted | Action, time, medication, dose, route, and recorded EMS note kept together |
| Read context and uncertainty | Sparse demographic summary and unexplained dashes | Visible EMS narrative and explicit “Not recorded” states |
| Confirm report delivery | Receipt sits inside the clinical summary | Distinct receipt section with eligibility explanation, original receiver/time, and bounded meaning |
| Inspect supporting data | Export competes with clinical content | Activity and structured export remain available in secondary disclosures |

These layout choices are engineering inferences from the communication priorities, not prescriptions contained in the sources.

## Missing information must remain visibly missing

“Latest observations” means the newest recorded observation row, even when some measurements are absent. **Do not silently fill its missing values from older rows**: that would create an apparent simultaneous assessment that was never recorded. Report-update time, observation time, treatment time, and receipt time describe different events and need distinct labels. Transport status must not be styled or described as a clinical acuity assessment.

PulseLink lacks dedicated fields for precise patient identity, allergies, usual medications, medical history, symptom onset, ETA, and treatment response. Notes can contain relevant context, but absent structured data must not become “none,” “normal,” or “no known allergies.” An empty intervention list means no interventions are recorded, not that none occurred. The interface therefore cannot claim a complete clinical handoff or mnemonic compliance.

## Compare retrieval before judging clinical usefulness

Use identical incidents and viewport sizes to compare the old and new screens. Ask reviewers to locate the complaint and source agency; report the latest blood pressure and its measurement time; identify a missing measurement; find an intervention’s dose, route, note, and time; and determine whether the report was received, by whom, and when. Include both incomplete observations and multiple observation sets. Record correct answers, visible placement, required expansions, and scrolling—not invented clinical-efficiency scores.

Separately verify keyboard access, narrow-screen readability, refresh failures, pending acknowledgment, original receipt preservation, and the hospital’s read-only boundary. Then test the same tasks with EMS and ED professionals, including whether the information order matches their local handoff process. AHRQ recommends developing and refining handoff tools with the actual team; that participation is the next validation step before claiming operational suitability. ([AHRQ handoff tool](https://www.ahrq.gov/teamstepps-program/curriculum/communication/tools/handoff.html))

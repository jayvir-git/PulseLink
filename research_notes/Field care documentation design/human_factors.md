# Paramedic ePCR human factors

## How does field work change documentation order and timing?

### Takeaway
Support interrupted, incremental recording rather than assuming a single uninterrupted form-filling session. Event time and entry time must remain distinct.

### Cited Findings
- The UK ERA study used multiple methods, including case studies at four ambulance services. Clinicians varied the timing and order of data entry and use of free text. Observations were sometimes held in memory or temporary notes before later electronic entry; researchers interpreted this as a response to practical constraints. Navigation also made it harder for some participants to remember what they had already entered. The study found limited evidence of impact on patient care, so digitisation itself should not be presented as a demonstrated clinical benefit. — [Porter et al., 2020, cross-cutting results](https://www.ncbi.nlm.nih.gov/books/NBK554285/)
- A Danish mixed-methods study combined 2016–2020 records with interviews and observation. Participants described ad hoc entry, touchscreen faults shortening prose, and restrictive automatic timestamps preventing accurate entry of earlier onset. Authors suggested prioritising field sequence. Mandatory red markers could imply completeness despite not matching local guidance. — [Kjær et al., 2023](https://journals.plos.org/plosone/article/file?id=10.1371/journal.pone.0293577&type=printable)

### Inferences
- Put observation and treatment entry within easy reach, before long historical lists. Keep section order stable and permit direct navigation among them.
- Keep saved history visible alongside, or immediately below, each entry task to reduce uncertainty about what has already been recorded.
- Do not call a server-generated entry timestamp the time care occurred. If event-time entry is outside the current model, say so and support documenting an actual earlier time in narrative without implying a structured timestamp exists.
- Avoid a percentage-complete score or green clinical-ready badge based solely on filled fields.

### Gaps
- These studies are UK/Danish, not direct validation of US crews or PulseLink. They do not establish a universally optimal screen order or touch-target size.

## What supports rapid capture and continuity with the receiving screen?

### Takeaway
Reduce navigation and preserve concise clinical context. Consistent information order across the sender and receiver is a defensible hypothesis to test, not a proven property of this application.

### Cited Findings
- A Scottish single-site pre/post feasibility study evaluated an A6 prompt card and corresponding ED form. Twenty-five ambulance clinicians returned follow-up surveys; 19 reported using the card to record information. The study found favourable acceptability, but incomplete ED handover forms prevented the intended objective handover comparison. Authors identified mismatched variable ordering between sender and receiver forms as a possible explanation for absent improvement in some perceived handover domains. The intervention was low-tech and does not establish efficacy of a digital layout. — [Fitzpatrick et al., 2018](https://link.springer.com/article/10.1186/s12873-018-0168-3)
- Danish respondents requested free-text headings and more flexible response options; predetermined fields did not always accommodate their information. — [Kjær et al., 2023](https://journals.plos.org/plosone/article/file?id=10.1371/journal.pone.0293577&type=printable)

### Inferences
- Reuse the hospital screen's labels and sequence for observations, treatments, and EMS notes, while giving the field screen entry controls first.
- Give treatment notes a multiline space and a concise prompt for observed response/reassessment, without generating clinical assertions.
- Provide narrative prompts as guidance, never prefilled normal findings or claims that an assessment occurred.
- Keep required inputs proportionate: an observation can be partial; absent is not normal, zero, or explicitly negative.

### Gaps
- No reviewed source establishes that replacing current PulseLink panels with a particular layout improves clinical outcomes. Evaluate with working paramedics using realistic interrupted tasks and the actual intended devices.

## How should save certainty and recovery be represented?

### Takeaway
Make the application's actual persistence state explicit and preserve entered work during recoverable failures. This is a design inference from observed field constraints, not a tested intervention from the cited studies.

### Cited Findings
- The Danish study reports frozen or slow tablets and restarts as obstacles to completing records. Its studied system had specific synchronisation capabilities; these cannot be attributed to PulseLink. — [Kjær et al., 2023](https://journals.plos.org/plosone/article/file?id=10.1371/journal.pone.0293577&type=printable)
- NIST's EHR user-centred design guidance calls for usability methods and testing in the development process; it is general EHR guidance, not EMS-specific validation. — [NISTIR 7741](https://www.nist.gov/publications/nistir-7741-nist-guide-processes-approach-improving-usability-electronic-health-records)

### Inferences
- Distinguish unsaved input, saving, confirmed saved, and uncertain result in plain language. A filled field is not a saved record.
- Retain drafts after validation, network, or concurrency failures. For an uncertain intervention result, retry the original request identity rather than creating a second treatment.
- Warn before leaving or closing editing with unsaved work. Keep final handoff separate from ordinary recording and make its consequences explicit.
- Do not advertise offline support, autosave, local persistence, or live hospital delivery unless implemented and verified. A useful present-tense statement is that unsaved inputs remain only while this page stays open, if that matches implementation.
- Test interrupted entry, failed save, uncertain response, concurrent edits, and handoff with a draft—not only successful form submissions.

### Gaps
- No source reviewed directly tests these particular save-state controls in EMS. Their value needs user evaluation. Production offline capture requires a separate persistence, reconciliation, privacy, and device-loss design.

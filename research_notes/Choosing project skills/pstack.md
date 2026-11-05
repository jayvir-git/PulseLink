# Lauren Tan's pstack

## Who made it and where does it run?

### Takeaway
Lauren Tan (@poteto) published pstack in Cursor's official plugin repository. [Original pull request](https://github.com/cursor/plugins/pull/73)

### Cited Findings
- The upstream README documents Cursor and an official Claude Code adapter. The adapter skips Cursor-only steps such as Bugbot and Automations. A reasoning budget and model choices are configurable. [Upstream README](https://github.com/cursor/plugins/tree/main/pstack#install)

### Inferences
- A fit assessment depends partly on which agent tools the user can run.

### Gaps
- No head-to-head performance measurements were assessed.

## How does it tackle a job?

### Takeaway
You describe the engineering outcome to `/poteto-mode`. It selects a playbook and calls supporting skills for design, implementation and verification. [Workflow guide](https://github.com/cursor/plugins/tree/main/pstack#usage)

### Cited Findings
- Playbooks cover investigation, features, performance, bugs, release supervision, and more. The feature playbook assigns design and verification to a lead, scopes implementation for delegates, and looks for separable work. [Feature playbook](https://github.com/cursor/plugins/blob/main/pstack/skills/poteto-mode/playbooks/feature.md)
- The bug workflow reproduces on the affected surface, tests candidate causes, applies the smallest supported fix and reruns the original reproduction. Expected output includes actual failing and passing results. [Bug-fix playbook](https://github.com/cursor/plugins/blob/main/pstack/skills/poteto-mode/playbooks/bug-fix.md)

### Inferences
- Parallel design and independent review buy additional scrutiny at the cost of additional agent runs and coordination. No universal cost or speed estimate follows.

### Gaps
- Actual behavior and spend depend on the workspace, models, task and available permissions.

## Where would it fit PulseLink?

### Takeaway
An illustrative use is chasing a handoff that appears in the wrong hospital's list. Verify the symptom under the affected hospital login, locate the API or UI cause, then exercise the same accounts before and after the fix. [Bug-fix playbook](https://github.com/cursor/plugins/blob/main/pstack/skills/poteto-mode/playbooks/bug-fix.md)

### Cited Findings
- A babysit playbook can manage open PR review, conflicts and CI until merge-ready. [Upstream playbooks](https://github.com/cursor/plugins/tree/main/pstack#usage)

### Inferences
- Best illustration is an existing cross-layer problem with a real reproducer. This is a hypothetical PulseLink task.

### Gaps
- The research did not execute any stack on PulseLink.

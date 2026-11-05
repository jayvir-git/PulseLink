# Choose skills for the work ahead

**Lauren Tan’s pstack helps an agent lead an engineering task. Theo Browne’s tstack helps it deliver a verified pull request. Matt Pocock’s skills help you decide what the software should do and build it in testable steps.** All three can help with implementation and review. What changes is the starting point, the decisions you keep, and the evidence you expect at the end. ([pstack](https://github.com/cursor/plugins/tree/main/pstack), [tstack](https://github.com/t3dotgg/tstack), [Matt’s skills](https://github.com/mattpocock/skills))

| Collection | What you give it | What you get back |
|---|---|---|
| **pstack** | An outcome or symptom. Its main command, `/poteto-mode`, selects an approach and coordinates the work. | For a bug, a reproduction, a supported explanation, a fix and evidence from rerunning the original case. ([Bug playbook](https://github.com/cursor/plugins/blob/main/pstack/skills/poteto-mode/playbooks/bug-fix.md)) |
| **tstack** | A concrete change and a decision about who will merge it. | A reviewed GitHub PR with evidence and checks. `full-stop` leaves it open for a human. `full-send` proceeds to merge when its conditions pass. ([Full-stop](https://github.com/t3dotgg/tstack/blob/main/skills/full-stop/SKILL.md), [full-send](https://github.com/t3dotgg/tstack/blob/main/skills/full-send/SKILL.md)) |
| **Matt’s skills** | A design question, a plan, a ticket or one behavior to test. You choose the relevant practice. | Depending on the skill, a glossary or decision record, a spec, independently verifiable tickets, or tested and reviewed code. ([Engineering catalog](https://github.com/mattpocock/skills#engineering)) |

## One PulseLink handoff, three different jobs

Suppose you are working on hospital handoff visibility. If people disagree about **when the receiving hospital should first see an incident**, Matt’s `/grill-with-docs` helps settle the rule and record what the terms mean. Once agreed, `/tdd` can exercise the rule through the hospital-facing API, one behavior at a time. ([Design interview](https://github.com/mattpocock/skills/blob/main/skills/engineering/grill-with-docs/SKILL.md), [TDD](https://github.com/mattpocock/skills/blob/main/skills/engineering/tdd/SKILL.md))

If the visibility rule is clear but **Hospital B can see Hospital A’s handoff**, pstack fits the investigation. Give it the symptom. Its bug playbook directs the agent to reproduce the problem where it happens, narrow down the cause, make a targeted change and repeat the same check. For this example, the convincing demonstration would use both hospital accounts and show that fixing the leak preserves the receiving hospital’s access. ([Bug playbook](https://github.com/cursor/plugins/blob/main/pstack/skills/poteto-mode/playbooks/bug-fix.md))

If the fix is understood and **you want someone to own delivery through CI and review**, tstack’s `full-stop` can finish the change, gather evidence, seek review and work through failing checks. You get the PR and its remaining blockers, if any, and keep the merge decision. Its full workflow assumes authenticated GitHub tooling and access to the reviewer models. ([Full-stop](https://github.com/t3dotgg/tstack/blob/main/skills/full-stop/SKILL.md), [requirements](https://github.com/t3dotgg/tstack#requirements))

## Where I’d start on PulseLink

I’d use **Matt’s skills for ordinary feature work** because PulseLink already has explicit roles, status transitions and a test suite. A small change can start directly with a behavior test. I’d reach for **pstack** when the cause spans the UI, API or database and needs investigation, and **tstack** when the time-consuming part is getting a known change reviewed and ready to merge. For this Codex workspace there’s also a practical distinction: Matt and Theo document Codex support, while Lauren documents Cursor and a Claude Code adapter with some Cursor-specific steps omitted. ([Matt’s setup](https://github.com/mattpocock/skills#installation-30-second-setup), [tstack](https://github.com/t3dotgg/tstack), [pstack setup](https://github.com/cursor/plugins/tree/main/pstack#install))

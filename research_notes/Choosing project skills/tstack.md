# Theo Browne's tstack

## What does it cover?

### Takeaway
Theo packages how he takes a concrete software change through a GitHub pull request, with a choice about who performs the final merge.

### Cited Findings
- The author's account identifies Theo Browne as t3dotgg. The collection supports Claude Code and Codex. Its nine skills cover proof and PR delivery, multi-model review, maintaining existing PRs, and PR communication. [Author profile](https://github.com/t3dotgg) and [official catalog](https://github.com/t3dotgg/tstack)
- `full-send` explicitly finishes the requested change, obtains evidence, uses `sol-loop` for review, opens a concise PR, monitors checks and addresses real failures, then merges after its conditions pass. The output includes PR link, merge commit and evidence. [Full-send workflow](https://github.com/t3dotgg/tstack/blob/main/skills/full-send/SKILL.md)
- `full-stop` follows that process through readiness and leaves the PR open for a person to merge. It reports whether the PR is ready, waiting on required approval, or blocked. [Full-stop workflow](https://github.com/t3dotgg/tstack/blob/main/skills/full-stop/SKILL.md)
- `council` seeks multiple providers' opinions. The full workflow calls related skills. The published prerequisites include an authenticated GitHub CLI, Python 3 for the PR watcher, and a harness able to run the reviewer models used by `council` and `sol-loop`. [Requirements, dependencies and catalog](https://github.com/t3dotgg/tstack#requirements)

### Inferences
- PulseLink illustration: given a defined change to hospital handoff visibility, use the project’s HTTP role-scope tests and CI as evidence, open the PR, fix failures and hand the green PR to a maintainer with full-stop. This assumes the reviewer tools are available. The listed capabilities center on getting code reviewed and shipped, with little dedicated product discovery.

### Gaps
- The assessment concerns published workflows. Their runtime quality and access to the reviewer models in this workspace were not tested.

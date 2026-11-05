# Matt Pocock's skills

## What does the collection optimize for?

### Takeaway
Small engineering practices can be used separately or composed into feature delivery. The developer settles terminology and behavior with the agent, then verifies the result.

### Cited Findings
- The library offers user-invoked orchestration and reusable disciplines. Its repo setup selects an issue tracker (local files are an option), labels and document location. The README documents Codex support. [Repository and reference](https://github.com/mattpocock/skills)
- `/grill-with-docs` combines interviewing and domain modeling. Its outputs can include a project glossary and architecture decision records. [Skill definition](https://github.com/mattpocock/skills/blob/main/skills/engineering/grill-with-docs/SKILL.md)

### Inferences
- The payoff for PulseLink is resolving domain rules people and agents might otherwise interpret differently.

### Gaps
- These describe intended behavior of the skills. No project-specific productivity benchmark was evaluated.

## How does work move from idea to tested code?

### Takeaway
A developer can combine discussion, spec, small tickets and test-driven implementation, or invoke one practice for a bounded problem.

### Cited Findings
- `/to-spec` captures an agreed conversation as a spec. `/to-tickets` proposes independently verifiable changes, records dependencies, and publishes the approved tickets. [Catalog](https://github.com/mattpocock/skills#engineering) and [ticket skill](https://github.com/mattpocock/skills/blob/main/skills/engineering/to-tickets/SKILL.md)
- `/implement` uses TDD and code review. `/tdd` agrees which public interface to test and works one failing behavior at a time. `/diagnosing-bugs` is an alternative entry point for an existing bug. [Catalog](https://github.com/mattpocock/skills#engineering) and [TDD skill](https://github.com/mattpocock/skills/blob/main/skills/engineering/tdd/SKILL.md)

### Inferences
- PulseLink example: clarify what “arrived” means versus “handed off,” then test the status API’s response when a paramedic tries to transport a patient without a destination. This uses the existing project guardrail as the expected behavior. A straightforward regression can start directly with diagnosis or TDD.

### Gaps
- The reviewed skills supply methods. The team must still decide what behavior is correct and supply a runnable test environment.

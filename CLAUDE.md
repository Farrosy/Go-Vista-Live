# CLAUDE.md

Project-specific guidance for Claude Code in this repository.

## Delegation & cost

- For delegable, mechanical work that has clear acceptance criteria — searching, reformatting, extracting, bulk multi-file edits from a plan, bounded research, collecting command output — proactively use the **`route`** skill before dispatching to subagents, so the work routes to the cheapest capable model under review instead of running everything on the top model.
- Keep judgment on the current model: architecture, ambiguous debugging, and the final review of any routed output stay here. If a task's acceptance criteria can't be stated in ~two sentences, don't route it.
- When unsure whether something is worth routing, prefer routing the clearly mechanical parts and keeping the rest.

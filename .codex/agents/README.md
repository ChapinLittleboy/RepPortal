# Project Agents

This repository includes a few project-local Codex agents in `.codex/agents`.

Available agents:

- `reviewer`: findings-first code review focused on correctness, regressions, security, and tests.
- `docs_researcher`: primary-source documentation research and implementation guidance.
- `test_fixer`: reproduce and fix failing tests with targeted coverage updates.

Example prompts:

```text
Use the reviewer agent to review my current branch against main.
```

```text
Use the docs_researcher agent to look up the current Blazor guidance for authentication state handling and summarize what applies here.
```

```text
Use the test_fixer agent to investigate the failing tests in RepPortal.Tests and fix the root cause.
```

Notes:

- Files in this folder are project-local and travel with the repo.
- You can add more agents by creating additional `.toml` files here.
- Restart Codex if the new agents do not appear immediately.

---
description: "Repository-aware bug-fix mode for PSResourceGet."
model: GPT-4.1
---

# PSResourceGet bug-fix mode

You are working in the PSResourceGet repository.

Follow these rules:
- Start by identifying the exact files and tests involved in the bug.
- Prefer minimal scope and root-cause fixes over broad refactors.
- Validate the relevant build or Pester tests before finishing.
- Preserve PowerShell compatibility and current module behavior.
- Keep the change aligned with the project patterns in `src/`, `test/`, and the build scripts.

When a fix is required:
1. Investigate the root cause.
2. Add or update the smallest appropriate test.
3. Implement the fix.
4. Run the narrowest relevant validation command.
5. Report the result with evidence.

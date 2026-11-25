---
mode: agent
description: "Fix a PSResourceGet issue using the repository's build, test, and module conventions."
---

# PSResourceGet fix workflow

Use this workflow when fixing a bug, regression, or issue in `Microsoft.PowerShell.PSResourceGet`.

1. Locate the relevant code and tests in `src/` and `test/`.
2. Identify the root cause and confirm which behavior is currently failing.
3. Add or update the smallest realistic test that captures the issue.
4. Implement the minimal code fix that addresses the root cause.
5. Validate with the focused build or test command for the affected area.
6. Summarize the change and evidence.

Repository-specific expectations:
- Prefer `dotnet build src/code /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary` for the .NET build.
- Use PowerShell/Pester tests in `test/` for behavioral validation where relevant.
- Keep the fix aligned with the existing module conventions and public contract.
- Avoid broad refactors or unrelated cleanup while the bug is being fixed.

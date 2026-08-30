---
applyTo: "src/**/*.cs,src/**/*.ps1,test/**/*.ps1,**/*.psm1,**/*.psd1,**/*.md"
---

# PSResourceGet repository instructions

- Treat this repo as the PowerShell module `Microsoft.PowerShell.PSResourceGet`.
- Favor small, targeted fixes that match the existing module and .NET conventions.
- Preserve compatibility with supported PowerShell versions and public behavior.
- Prefer repo-local build and validation commands before introducing new workflows.
- When fixing issues, identify the root cause, add or update the relevant test, and validate the narrowest scope possible.
- Keep changes limited to the issue being addressed; avoid unrelated refactors.
- For packaging or module behavior changes, validate the relevant Pester files under `test/` and ensure the project still builds.

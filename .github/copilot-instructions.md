# Copilot instructions for PSResourceGet

## Project context
- This repository contains `Microsoft.PowerShell.PSResourceGet`, a PowerShell module for discovering, installing, updating, and publishing PowerShell resources.
- Source code lives under `src/code` and the compiled module is packaged from `src/Microsoft.PowerShell.PSResourceGet.psm1` and related assets.
- Tests live under `test/` and are typically PowerShell-based Pester tests.

## Build and validation
- Build the .NET project with:
  - `dotnet build src/code /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary`
- For repo-local builds, prefer the existing build scripts: `build.ps1` for quick builds or `doBuild.ps1` for comprehensive builds.
- Run focused validation with PowerShell/Pester for the relevant test area before claiming a fix is complete.
- If a change affects packaging or module behavior, validate the relevant Pester files in `test/` and ensure the build still succeeds.

## Coding guidance
- Keep changes aligned with the existing PowerShell module and .NET project conventions.
- Favor small, targeted fixes with clear names and minimal scope.
- Preserve compatibility with supported PowerShell versions and the current public module behavior.
- Prefer existing helper patterns and repository conventions over introducing new abstractions.

## Scope for agent work
- When asked to fix a bug, first identify the relevant files and tests, then implement the minimum fix that addresses the root cause.
- When adding or modifying behavior, make sure the corresponding tests reflect the change and remain realistic.
- When working on release or package concerns, respect repository build scripts and the module packaging expectations under `src/`.

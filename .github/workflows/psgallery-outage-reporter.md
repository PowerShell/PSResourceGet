---
description: |
  Reports PowerShell Gallery outages. Triggered by the 'PowerShell Gallery Health Check'
  workflow when gallery endpoints keep returning 404 or 5xx. Re-verifies the failure, then
  opens (or updates) a service-status issue in PowerShell/PowerShellGallery so the status
  page at https://aka.ms/psgallery can be updated.

on:
  workflow_dispatch:
    inputs:
      probe_results:
        description: 'JSON array of failing probes reported by the health check'
        required: true
        type: string
      health_check_run_url:
        description: 'URL of the health check run that detected the failure'
        required: false
        type: string

permissions:
  contents: read
  issues: read

engine: copilot

timeout-minutes: 15

network:
  allowed:
    - defaults
    - powershell

tools:
  github:
    toolsets: [issues]
  bash: ["curl:*", "jq:*"]

safe-outputs:
  # PAT or GitHub App token with 'issues: write' on PowerShell/PowerShellGallery.
  github-token: ${{ secrets.PSGALLERY_STATUS_ISSUE_TOKEN }}
  create-issue:
    target-repo: "alerickson/PSResourceGet"
    title-prefix: "[Service Status] "
    max: 1
  add-comment:
    target: "*"
    target-repo: "alerickson/PSResourceGet"
    max: 1
---

# PowerShell Gallery outage reporter

You are an on-call assistant for PSResourceGet. The scheduled health check detected that one or
more PowerShell Gallery endpoints repeatedly returned HTTP 404 or 5xx responses.

Failing probes reported by the health check (JSON):

```json
${{ github.event.inputs.probe_results }}
```

Health check run: ${{ github.event.inputs.health_check_run_url }}

## Steps

1. **Re-verify.** For every failing probe, request its `uri` again with
   `curl --silent --location --output /dev/null --max-time 60 --write-out '%{http_code}' '<uri>'`,
   up to 3 times, about 20 seconds apart. Treat a probe as still failing only if every attempt
   returns 404 or a status code of 500 or higher. If no probe is still failing, the incident has
   recovered: do **not** create an issue or comment; finish by reporting that no action was needed.

2. **Look for an existing report.** Search open issues in `alerickson/PSResourceGet` whose
   title starts with `[Service Status]` and that were created in the last 24 hours. Also consider
   any open issue that clearly describes the same outage (same endpoints / status codes).

3. **Report.**
   - If a matching open issue exists, add **one** comment to it with the latest re-verification
     results (timestamp in UTC, endpoint, status code). Keep it short.
   - Otherwise create **one** issue in `alerickson/PSResourceGet`:
     - Title: a short summary such as `PowerShell Gallery returning HTTP 503 for package downloads`
       (the `[Service Status]` prefix is added automatically).
     - Body must include:
       - A one-paragraph summary of the impact on users (for example: `Find-PSResource` /
         `Install-PSResource` / `Save-PSResource` against `PSGallery` fail).
       - A Markdown table of still-failing probes: probe, URI, HTTP status, time checked (UTC).
       - Any probes from the health check that recovered during re-verification.
       - A link to the health check run (above).
       - A request to the PowerShell Gallery team to update the service status at
         [aka.ms/psgallery](https://aka.ms/psgallery) and to close this issue once resolved.
       - A note that this issue was opened automatically by the PSResourceGet health monitor.

## Rules

- Never include secrets, tokens, or request headers in the issue or comment.
- Never create more than one issue or more than one comment per run.
- Do not speculate about root cause; report only what you observed.

---
description: |
  Reports PowerShell Gallery outages. Triggered by the 'PowerShell Gallery Health Check'
  workflow when gallery queries return too many 404/5xx or timed-out responses. Re-verifies the
  failure, then opens a pull request in PowerShell/PowerShellGallery that adds an "Ongoing" entry to
  psgallery_status.md (or comments on an existing one) for the Gallery team to review and merge.

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
  pull-requests: read

engine: copilot

timeout-minutes: 15

# Only the PowerShell Gallery (subdomains included) for the agent's curl re-checks. GitHub and Copilot
# traffic is allowed separately by gh-aw; 'defaults' is omitted because it adds package mirrors and
# other hosts this workflow doesn't need.
network:
  allowed:
    - powershellgallery.com

checkout:
  - repository: PowerShell/PowerShellGallery
    ref: master
    path: PowerShellGallery
    current: true

tools:
  github:
    toolsets: [pull_requests, issues]
  edit:
  bash: ["curl:*", "jq:*", "sleep", "date", "cat:*", "head:*"]

safe-outputs:
  # PAT or GitHub App token with 'contents: write' and 'pull-requests: write' on
  # PowerShell/PowerShellGallery ('issues: write' too, for comments and the issue fallback).
  github-token: ${{ secrets.PSGALLERY_STATUS_PR_TOKEN }}
  create-pull-request:
    target-repo: "PowerShell/PowerShellGallery"
    base-branch: master
    title-prefix: "[Service Status] "
    draft: false
    allowed-files: ["psgallery_status.md"]
    max: 1
  add-comment:
    target: "*"
    target-repo: "PowerShell/PowerShellGallery"
    max: 1
---

# PowerShell Gallery outage reporter

You are an on-call assistant for PSResourceGet. The scheduled health check detected that one or
more PowerShell Gallery endpoints returned too many bad responses (HTTP 404, 5xx, or a transfer that
timed out or failed within 60 seconds). Each probe mirrors a query PSResourceGet sends for
`Find-PSResource`. Outages are often partial: a single unhealthy instance behind the load balancer
fails only a fraction of requests, so a probe that succeeds some of the time can still be failing.

Each reported probe includes `samples` (requests sent), `failures` (bad responses), `statusCodes`
(distribution of results: an HTTP status code, or `curlNN` when curl itself failed with exit code NN,
for example `curl28` for a timeout, even if the server had already sent a 200 status) and
`slowestSeconds` (informational only; slow responses that succeed are not failures).

Failing probes reported by the health check (JSON):

```json
${{ github.event.inputs.probe_results }}
```

Health check run: ${{ github.event.inputs.health_check_run_url }}

The `PowerShell/PowerShellGallery` repository is checked out in the `PowerShellGallery` directory.
Its `psgallery_status.md` file is the public PowerShell Gallery status page.

## Steps

1. **Re-verify.** For every failing probe, request its `uri` again with
   `curl --silent --location --output /dev/null --max-time 60 --write-out '%{http_code} %{time_total} %{exitcode}' '<uri>'`,
   up to 4 times (no sleep needed between attempts). The output is the HTTP status code, the time in
   seconds, and curl's exit code. A response is bad if curl's exit code is not `0` (for example `28`
   when the transfer timed out, even if the status code is `200`), or the status code is `000`, 404,
   or 500 or higher; slow responses that complete successfully are fine. Stop re-checking a probe as soon
   as you see a bad response. Treat a probe as still failing if **any** attempt was bad; it has
   recovered only if all 4 attempts were good. If no probe is still failing, the incident has
   recovered: do **not** open a pull request or comment; finish by reporting that no action was needed.

   Shell commands are restricted to an allowlist: run each `curl`, `sleep`, and `date` invocation as
   its own separate command. Do not use loops, pipes, `;`, `&&`, subshells, or variables.

   Every probe URI targets a package or endpoint that is expected to exist, so a 404 is a failure.
   Do not second-guess whether a 404 is "expected" based on the package name.

2. **Look for an existing report.**
   - Search open pull requests in `PowerShell/PowerShellGallery` whose title starts with
     `[Service Status]` and that were created in the last 24 hours.
   - Read `PowerShellGallery/psgallery_status.md`. If its newest entry is marked `__Status: Ongoing__`,
     is dated today or yesterday (UTC), and describes the same kind of problem (for example, search or
     install failures), the Gallery team has already posted this outage.

3. **Report.**
   - If a matching open pull request exists, add **one** comment to it with the latest re-verification
     results (timestamp in UTC, endpoint, bad responses out of attempts, status codes). Keep it short.
   - Otherwise, if the status page already has a matching ongoing entry, do nothing and finish by
     reporting that the outage is already posted.
   - Otherwise open **one** pull request against `master` that changes only `psgallery_status.md`:
     - **Edit:** add a new entry directly below the `=========================` line under the
       `PowerShell Gallery Status` heading, above all existing entries, separated by blank lines.
       Do not change any existing entry. Match the existing entries' format exactly:

       ```markdown
       ### Nov 13th 2026, Errors when searching for packages.
       Since 9:00pm UTC we have been observing intermittent errors and timeouts when searching for packages (for example, `Find-PSResource -Tag` and `Find-PSResource -Name` with wildcards). We are investigating, but you may see failures or delays when searching for or installing packages.

       __Status: Ongoing__
       ```

       Use the UTC date and time of the earliest failure you observed (`checkedAt` from the health check):
       the date in the `<Mon> <day><ordinal> <yyyy>` style and the time rounded down to the hour, so the
       heading's date matches the "Since" time even if the run happens after midnight UTC. Write a
       short, user-facing description of the impact. Do not include probe URIs, status-code tables,
       internal details, or speculation about the cause in the status page entry.
     - **Title:** a short summary such as `Errors when searching for packages` (the `[Service Status]`
       prefix is added automatically).
     - **Body** must include:
       - A one-paragraph summary of the impact on users (for example: `Find-PSResource` /
         `Install-PSResource` / `Save-PSResource` against `PSGallery` fail).
       - A Markdown table of still-failing probes: probe, URI, bad responses out of samples (from
         both the health check and your re-verification), status codes seen, slowest response time,
         time checked (UTC).
       - If only some requests failed, state that the outage appears intermittent (for example, a
         single unhealthy instance) rather than a full outage.
       - Any probes from the health check that recovered during re-verification.
       - A link to the health check run (above).
       - A request to the PowerShell Gallery team to review the wording, merge this pull request to
         publish the status, and update the entry to `__Status: Resolved__` once the issue is fixed.
       - A note that this pull request was opened automatically by the PSResourceGet health monitor.

## Rules

- Never include secrets, tokens, or request headers in the pull request, status entry, or comment.
- Never open more than one pull request or post more than one comment per run.
- Only modify `psgallery_status.md`; never change any other file.
- Do not speculate about root cause; report only what you observed.
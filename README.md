# PSResourceGet

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/PowerShell/PSResourceGet/blob/master/LICENSE)
[![Documentation - PSResourceGet](https://img.shields.io/badge/Documentation-PowerShellGet-blue.svg)](https://learn.microsoft.com/powershell/module/microsoft.powershell.psresourceget)
[![PowerShell Gallery - PSResourceGet](https://img.shields.io/badge/PowerShell%20Gallery-PSResourceGet-blue.svg)](https://www.powershellgallery.com/packages/Microsoft.PowerShell.PSResourceGet)
[![Minimum Supported PowerShell Version](https://img.shields.io/badge/PowerShell-5.0-blue.svg)](https://github.com/PowerShell/PSResourceGet)

## Important Notes

> [!NOTE]
> `PSResourceGet` is short for the full name of the module, `Microsoft.PowerShell.PSResourceGet`.  The full name is what is used in PowerShell and when published to the [PowerShell Gallery](https://www.powershellgallery.com/packages/Microsoft.PowerShell.PSResourceGet).

* If you were familiar with the PowerShellGet 3.0 project, we renamed the module to be PSResourceGet, for more information please read [this blog](https://devblogs.microsoft.com/powershell/powershellget-in-powershell-7-4-updates/).
* If you would like to open a PR please open an issue first so that necessary discussion can take place.
  * Please open an issue for any feature requests, bug reports, or questions for PSResourceGet.
  * See the [Contributing Quickstart Guide](#contributing-quickstart-guide) section.
* Please note, the repository for PowerShellGet v2 is available at [PowerShell/PowerShellGetv2](https://github.com/PowerShell/PowerShellGetv2).
* The repository for the PowerShellGet v3, the compatibility layer between PowerShellGet v2 and PSResourceGet, is available at [PowerShell/PowerShellGet](https://github.com/PowerShell/PowerShellGet).

## Introduction

PSResourceGet is a PowerShell module with commands for discovering, installing, updating and publishing the PowerShell resources like Modules, Scripts, and DSC Resources.

## Documentation

[Click here](https://learn.microsoft.com/powershell/module/microsoft.powershell.psresourceget) to reference the documentation.

## Requirements

* PowerShell 5.0 or higher.

## Install the PSResourceGet module

* `PSResourceGet` is short for the full name `Microsoft.PowerShell.PSResourceGet`.
* It's included in PowerShell since v7.4.
Please use the [PowerShell Gallery](https://www.powershellgallery.com) to get the latest version of the module.

## Contributing Quickstart Guide

### Get the source code

* Download the latest source code from the release page (<https://github.com/PowerShell/PSResourceGet/releases>) OR clone the repository using git.
  ```powershell
  PS > cd 'C:\Repos'
  PS C:\Repos> git clone https://github.com/PowerShell/PSResourceGet
  ```
* Navigate to the local repository directory
  ```powershell
  PS C:\> cd c:\Repos\PSResourceGet
  PS C:\Repos\PSResourceGet>
  ```

### Build the project

Note:  Please ensure you have the exact version of the .NET SDK installed. The current version can be found in the [global.json](https://github.com/PowerShell/PSResourceGet/blob/master/global.json) and installed from the [.NET website](https://dotnet.microsoft.com/en-us/download).
  ```powershell
  # Build for the net472 framework
  PS C:\Repos\PSResourceGet> .\build.ps1 -Clean -Build -BuildConfiguration Debug -BuildFramework net472
  ```

### Run functional tests

* Run all tests
  ```powershell
  PS C:\Repos\PSResourceGet> Invoke-Pester
  ```
* Run an individual test
  ```powershell
  PS C:\Repos\PSResourceGet> Invoke-Pester <file-name>
  ```

### Import the built module into a new PowerShell session

```powershell
# If running PowerShell 6+
C:\> pwsh
C:\> Import-Module C:\Repos\PSResourceGet\out\PSResourceGet

# If running Windows PowerShell
c:\> PowerShell
C:\> Import-Module C:\Repos\PSResourceGet\out\PSResourceGet\PSResourceGet.psd1
```

### GitHub Actions CI

`.github/workflows/ci.yml` builds and packages the module, then executes the existing
Pester 4 CI tests on Windows (PowerShell 7 and Windows PowerShell), Ubuntu, and
macOS. Every run (pull requests targeting `master`, pushes to `master`, and manual
runs on any branch) uses the same five-job matrix: the complete CI suite on all
four platforms plus the ACR-only suite with AzAuth on Windows. There is no
credential-free subset or event-based test exclusion. The existing `CI` tag and
`ManualValidationOnly` exclusion are preserved. Failed tests fail the job; NUnit
XML is retained as an artifact even on failure. Runs are not automatically
cancelled or replaced by newer runs.

Before enabling authenticated runs:

1. Create a GitHub environment named `ci-integration`. Set deployment branches and
   tags to **No restriction** to support PR merge refs and manual runs on any
   branch. Configure **required reviewers**, enable **Prevent self-review**, and
   disable administrator bypass. Review the exact workflow, scripts, tests, and
   source revision before approval: all checked-out code can access the test
   credentials and Azure identity once the environment is approved. The previous
   master-only restriction must be removed; `ci-public` is no longer used.
2. Configure a Microsoft Entra application/service principal with a GitHub OIDC
   federated credential: issuer `https://token.actions.githubusercontent.com`,
   subject `repo:PowerShell/PSResourceGet:environment:ci-integration`, audience
   `api://AzureADTokenExchange`. No client secret is needed.
3. Add environment **variables** `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and
   `AZURE_SUBSCRIPTION_ID` for that identity and subscription. Add
   `CI_GITHUB_PACKAGES_USERNAME` (the GitHub PAT owner), `CI_ADO_USERNAME` (a nonempty
   ADO credential username), and `CI_ADO_PRIVATE_REPO_URL` (the private test feed's
   NuGet v3 index URL).
4. Add environment **secrets** `CI_GITHUB_PACKAGES_PAT`, `CI_ADO_PUBLIC_PAT`, and
   `CI_ADO_PRIVATE_PAT`. Use a classic GitHub PAT with `read:packages` and access to
   the PowerShell organization's `test_module` and `test_script` packages; authorize
   organizational SSO if required. ADO PATs need Packaging Read & write and Feed
   Publisher access to the corresponding test feeds. These replace the
   `GithubTestingFeedCreds` variable group. Never put tokens in variables.
5. Grant the Azure identity registry-scoped `AcrPush` and `AcrDelete` on
   `psresourcegettest`, and `AcrPull` on `psresourcegettestwildcard`, for registries in
   legacy RBAC mode. For ABAC-enabled registries, use Container Registry Repository
   Contributor on the main registry, Repository Reader on the wildcard registry,
   and Repository Catalog Lister on both, with conditions allowing the fixture and
   generated test repositories. Also grant registry-scoped `Reader` on
   `psresourcegettest` for Azure CLI's management-plane registry lookup.
   Ensure ARM-audience authentication is enabled.
   Add the service principal to the `powershell-rel` Azure DevOps organization,
   grant access to the `PSResourceGet` project, and grant Feed Reader on
   `psrg-credprovidertest`. Azure RBAC alone does not grant Azure Artifacts access.
6. Enable GitHub Actions and allow the pinned `actions/checkout`,
   `actions/setup-dotnet`, `actions/upload-artifact`, `actions/download-artifact`,
   and `azure/login` actions. The workflow grants `id-token: write` only to
   authenticated jobs; the default token otherwise only needs `contents: read`.
   DSC downloads use the automatically supplied GitHub token, replacing the
   `InstallDSC` variable group. No separate DSC token or Azure service connection
   is needed.

GitHub does not provide secrets or a writable OIDC token to fork pull-request
workflows (including Dependabot PRs); environment approval does not lift that
restriction. Those runs cannot complete authenticated tests and do not fall back
to a smaller suite. After reviewing the changes, a maintainer must create a branch
in this repository containing the reviewed revision and run CI there (a same-repo
PR or manual dispatch), then approve `ci-integration`. Never use
`pull_request_target` to check out and execute untrusted PR code with credentials.
The workflow fails when required credentials are unavailable rather than reporting
partial test coverage as success.

The existing test feeds and ACR registries must retain their fixture packages and
be reachable from GitHub-hosted runners. The registry and public-feed URLs are
hard-coded in the tests; changing environment variables does not retarget them.
AzAuth uses the Azure CLI session established by `azure/login`. Other ACR tests
use a short-lived ARM token in the runner-local SecretStore; credential-provider
tests use a separate Azure DevOps-audience token. Neither token is printed or
uploaded. ACR cleanup runs even after test failures, but a terminated/timed-out
runner can leave generated repositories requiring manual cleanup.

The Azure DevOps pipeline files are retained for transition and release consumers.
The external `PowerShell/compliance` stage is **not** ported by this test migration.
Keep the old compliance coverage until its owners approve a replacement. After
successful GitHub runs, replace Azure DevOps test branch-policy checks with the
five `Tests` checks and `Build package`. All five test jobs apply to PRs as well as
pushes and manual runs, and wait for the environment approval described above.

## Module Support Lifecycle 
Microsoft.PowerShell.PSResourceGet follows the support lifecycle of the version of PowerShell that it ships in. 
For example, PSResourceGet 1.0.x shipped in PowerShell 7.4 which is an LTS release so it will be supported for 3 years.
Preview versions of the module, or versions that ship in preview versions of PowerShell are not supported.
Versions of PSResourceGet that do not ship in a version of PowerShell will be fixed forward.

## Code of Conduct

Please see our [Code of Conduct](CODE_OF_CONDUCT.md) before participating in this project.

## Security Policy

For any security issues, please see our [Security Policy](SECURITY.md).

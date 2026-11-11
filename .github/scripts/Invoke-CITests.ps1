# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Full', 'AzAuth')]
    [string] $Suite
)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$config = Get-Content (Join-Path $root 'package.config.json') -Raw | ConvertFrom-Json
$testPath = Join-Path $root $config.TestPath
$modulePath = Join-Path (Join-Path $root $config.BuildOutputPath) $config.ModuleName
$acrFiles = @(
    'FindPSResourceContainerRegistryServer.Tests.ps1'
    'InstallPSResourceContainerRegistryServer.Tests.ps1'
    'PublishPSResourceContainerRegistryServer.Tests.ps1'
)
$files = @(Get-ChildItem $testPath -Recurse -Filter '*.Tests.ps1' -File | Where-Object {
    $Suite -eq 'Full' -or $_.Name -in $acrFiles
})
if ($files.Count -eq 0) { throw "No test files selected for suite $Suite." }

$env:USINGAZAUTH = ($Suite -eq 'AzAuth').ToString().ToLowerInvariant()
$repositoryNamesFolder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'TempModules'
$null = New-Item $repositoryNamesFolder -ItemType Directory -Force
$null = New-Item (Join-Path $repositoryNamesFolder 'ACRTestRepositoryNames.txt') -ItemType File -Force

if ($Suite -eq 'Full') {
    foreach ($name in 'TENANTID', 'GITHUB_USERNAME', 'ADO_USERNAME', 'MAPPED_GITHUB_PAT', 'MAPPED_ADO_PUBLIC_PAT', 'MAPPED_ADO_PRIVATE_PAT', 'MAPPED_ADO_PRIVATE_REPO_URL') {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            throw "Missing $name. Configure the ci-integration environment as described in README.md."
        }
    }
    Import-Module Microsoft.PowerShell.SecretManagement
    Import-Module Microsoft.PowerShell.SecretStore
    Set-SecretStoreConfiguration -Authentication None -Interaction None -Confirm:$false
    Register-SecretVault -Name SecretStore -ModuleName Microsoft.PowerShell.SecretStore -DefaultVault

    $acrToken = & az account get-access-token --resource 'https://management.azure.com/' --query accessToken --output tsv --only-show-errors
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($acrToken)) {
        throw 'Could not acquire an ARM token for the ACR SecretStore tests.'
    }
    Write-Host "::add-mask::$acrToken"
    Set-Secret -Name $env:TENANTID -Secret (ConvertTo-SecureString $acrToken -AsPlainText -Force) -Vault SecretStore

    # Azure DevOps needs its own token audience, not an ARM access token.
    $adoToken = & az account get-access-token --resource '499b84ac-1321-427f-aa17-267ca6975798' --query accessToken --output tsv --only-show-errors
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($adoToken)) {
        throw 'Could not acquire an Azure DevOps token for the credential provider tests.'
    }
    Write-Host "::add-mask::$adoToken"
    $env:VSS_NUGET_EXTERNAL_FEED_ENDPOINTS = @{
        endpointCredentials = @(@{
            endpoint = 'https://pkgs.dev.azure.com/powershell-rel/PSResourceGet/_packaging/psrg-credprovidertest/nuget/v2'
            username = 'ci'
            password = $adoToken
        })
    } | ConvertTo-Json -Compress -Depth 4
}

# Run in this fresh shell, never the process that imported the bootstrap module.
Import-Module Pester -RequiredVersion 4.10.1 -Force
Import-Module $modulePath -Force
Set-Location $testPath
$resultFile = Join-Path $testPath 'result.pester.xml'
try {
    # Preserve the existing suite's handling of expected non-terminating errors.
    $ErrorActionPreference = 'Continue'
    $result = Invoke-Pester -Script $files.FullName -Tag CI -ExcludeTag ManualValidationOnly `
        -OutputFormat NUnitXml -OutputFile $resultFile -PassThru
}
finally {
    $ErrorActionPreference = 'Stop'
}
if (-not (Test-Path $resultFile) -or $null -eq $result -or $result.TotalCount -eq 0) {
    throw "Suite $Suite did not produce test results."
}
if ($result.FailedCount -gt 0) {
    throw "$($result.FailedCount) Pester tests failed in suite $Suite."
}
# Expected native failures (for example DSC negative tests) must not fail the shell.
$global:LASTEXITCODE = 0

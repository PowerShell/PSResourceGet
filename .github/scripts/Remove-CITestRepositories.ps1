# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ErrorActionPreference = 'Stop'
$repositoryNamesFolder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'TempModules'
$repositoryNamesFile = Join-Path $repositoryNamesFolder 'ACRTestRepositoryNames.txt'
if (-not (Test-Path $repositoryNamesFile)) {
    Write-Warning 'ACR tests did not initialize their cleanup file; no repositories will be deleted.'
    return
}
$names = @(Get-Content $repositoryNamesFile | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
if ($names.Count -eq 0) {
    return
}
$repositories = & az acr repository list --name psresourcegettest --output json --only-show-errors
if ($LASTEXITCODE -ne 0) { throw 'Could not list ACR repositories for cleanup.' }
$repositories = $repositories | ConvertFrom-Json
foreach ($name in $names) {
    # Only delete GUID-suffixed packages produced by the publish tests, never fixtures.
    if ($name -notmatch '^temp-(testmodule|testmodulewithoutrequiredmodule-|testscript|testscriptwithexternaldeps|scriptwithoutemptylinesinmetadata|scriptwithoutemptylinesbetweencommentblocks)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$') {
        throw "Refusing to delete unexpected ACR repository name: $name"
    }
    if ($name -in $repositories) {
        & az acr repository delete --name psresourcegettest --repository $name --yes --only-show-errors
        if ($LASTEXITCODE -ne 0) { throw "Failed to delete test repository $name." }
    }
}

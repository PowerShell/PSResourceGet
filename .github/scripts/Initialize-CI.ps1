# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[CmdletBinding()]
param(
    [switch] $ForTest,
    [switch] $UseAzAuth
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$modulePath = Join-Path $env:RUNNER_TEMP 'TempModules'
$null = New-Item $modulePath -ItemType Directory -Force
Save-Module -Name Microsoft.PowerShell.PSResourceGet -MinimumVersion 0.9.0 -Path $modulePath -Force
$env:PSModulePath = $modulePath + [IO.Path]::PathSeparator + $env:PSModulePath
"PSModulePath=$env:PSModulePath" >> $env:GITHUB_ENV

if (-not $ForTest) {
    return
}

"TEMP=$env:RUNNER_TEMP" >> $env:GITHUB_ENV
Save-Module -Name Pester -RequiredVersion 4.10.1 -Path $modulePath -Force
if (-not $UseAzAuth) {
    Save-Module -Name Microsoft.PowerShell.SecretManagement, Microsoft.PowerShell.SecretStore -Path $modulePath -Force
}

$headers = @{
    'User-Agent' = 'PSResourceGet-CI'
    Accept = 'application/vnd.github+json'
    Authorization = "Bearer $env:GH_TOKEN"
}
$releases = Invoke-RestMethod 'https://api.github.com/repos/PowerShell/DSC/releases' -Headers $headers -MaximumRetryCount 3
$release = $releases | Where-Object { -not $_.draft } | Sort-Object published_at -Descending | Select-Object -First 1
$architecture = switch ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
    'X64' { 'x86_64' }
    'Arm64' { 'aarch64' }
    default { throw "Unsupported DSC architecture: $_" }
}
$platform = if ($IsWindows) { 'pc-windows-msvc' } elseif ($IsMacOS) { 'apple-darwin' } else { 'linux' }
$extension = if ($IsWindows) { '.zip' } else { '.tar.gz' }
$assets = @($release.assets | Where-Object { $_.name.EndsWith("-$architecture-$platform$extension") })
if ($assets.Count -ne 1) {
    throw "Expected one DSC asset for $architecture-$platform in release $($release.tag_name); found $($assets.Count)."
}
$dscPath = Join-Path $env:RUNNER_TEMP 'dsc'
$null = New-Item $dscPath -ItemType Directory -Force
$archive = Join-Path $env:RUNNER_TEMP $assets[0].name
Invoke-WebRequest $assets[0].browser_download_url -OutFile $archive
if ($IsWindows) {
    Expand-Archive $archive -DestinationPath $dscPath -Force
}
else {
    & tar -xzf $archive -C $dscPath
    if ($LASTEXITCODE -ne 0) { throw 'Failed to extract DSC.' }
}
$executableName = if ($IsWindows) { 'dsc.exe' } else { 'dsc' }
$executables = @(Get-ChildItem $dscPath -Recurse -File -Filter $executableName)
if ($executables.Count -ne 1) {
    throw "Expected one DSC executable; found $($executables.Count)."
}
& $executables[0].FullName --version
if ($LASTEXITCODE -ne 0) { throw 'DSC could not start.' }
"DSC_ROOT=$($executables[0].DirectoryName)" >> $env:GITHUB_ENV

if (-not $UseAzAuth) {
    # Windows PSResourceGet discovers an .exe; Unix discovers the netcore .dll.
    $assetName = if ($IsWindows) { 'Microsoft.NetFx48.NuGet.CredentialProvider.zip' } else { 'Microsoft.Net8.NuGet.CredentialProvider.tar.gz' }
    $providerArchive = Join-Path $env:RUNNER_TEMP $assetName
    $providerPath = Join-Path $env:RUNNER_TEMP 'credential-provider'
    $null = New-Item $providerPath -ItemType Directory -Force
    Invoke-WebRequest "https://github.com/microsoft/artifacts-credprovider/releases/download/v2.0.4/$assetName" -OutFile $providerArchive
    if ($IsWindows) {
        Expand-Archive $providerArchive -DestinationPath $providerPath -Force
    }
    else {
        & tar -xzf $providerArchive -C $providerPath
        if ($LASTEXITCODE -ne 0) { throw 'Failed to extract the credential provider.' }
    }
    $pluginsPath = Join-Path $HOME '.nuget/plugins'
    $null = New-Item $pluginsPath -ItemType Directory -Force
    Copy-Item (Join-Path $providerPath 'plugins/*') $pluginsPath -Recurse -Force
    $providerFile = if ($IsWindows) { 'netfx/CredentialProvider.Microsoft/CredentialProvider.Microsoft.exe' } else { 'netcore/CredentialProvider.Microsoft/CredentialProvider.Microsoft.dll' }
    if (-not (Test-Path (Join-Path $pluginsPath $providerFile))) {
        throw 'The credential provider was not installed in the expected discovery location.'
    }
}

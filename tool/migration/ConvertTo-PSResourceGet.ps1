# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Converts PowerShellGet v2 cmdlet usage to PSResourceGet equivalents.

.DESCRIPTION
    Scans PowerShell files (.ps1, .psm1, .psd1) or inline script strings for
    PSGet v2 cmdlet usage (e.g., Install-Module, Find-Module) and converts them
    to their PSResourceGet equivalents (e.g., Install-PSResource, Find-PSResource).

    By default, runs in WhatIf/report mode. Use -Apply to modify files.

.PARAMETER Path
    Path to a file or directory to scan. Supports wildcards.
    Defaults to the current directory. Cannot be used with -InputScript.

.PARAMETER InputScript
    A string containing PowerShell script text to convert. The converted string
    is returned as output. Cannot be used with -Path.

.PARAMETER Recurse
    When Path is a directory, scan subdirectories recursively.

.PARAMETER Apply
    Apply the changes to files in-place. Without this switch, only a report is generated.

.PARAMETER BackupPath
    Directory for backup copies. Defaults to .bak files alongside originals.

.PARAMETER PassThru
    Emit structured result objects to the pipeline instead of formatted output.

.EXAMPLE
    # Dry-run: scan current directory recursively and show migration report
    .\ConvertTo-PSResourceGet.ps1 -Path . -Recurse

.EXAMPLE
    # Apply changes with backups
    .\ConvertTo-PSResourceGet.ps1 -Path .\scripts -Recurse -Apply -BackupPath .\backups

.EXAMPLE
    # Scan a single file and get structured output
    .\ConvertTo-PSResourceGet.ps1 -Path .\deploy.ps1 -PassThru

.EXAMPLE
    # Convert a script string and get the converted text back
    .\ConvertTo-PSResourceGet.ps1 -InputScript 'Install-Module -Name Pester -Force'

.EXAMPLE
    # Pipe a string for conversion
    'Find-Module -Name Az -AllowPrerelease' | .\ConvertTo-PSResourceGet.ps1
#>

[CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'Path')]
param(
    [Parameter(Position = 0, ParameterSetName = 'Path')]
    [string] $Path,

    [Parameter(Mandatory, ParameterSetName = 'String', ValueFromPipeline)]
    [string] $InputScript,

    [Parameter(ParameterSetName = 'Path')]
    [switch] $Recurse,

    [Parameter(ParameterSetName = 'Path')]
    [switch] $Apply,

    [Parameter(ParameterSetName = 'Path')]
    [string] $BackupPath,

    [switch] $PassThru
)

# Import the migration module
$modulePath = Join-Path $PSScriptRoot 'PSGetMigration.psm1'
Import-Module $modulePath -Force

# String input mode
if ($PSCmdlet.ParameterSetName -eq 'String') {
    $result = ConvertTo-PSResourceGetString -InputScript $InputScript
    if ($PassThru) {
        $result
    }
    else {
        $result.ConvertedScript
    }
    return
}

# File/directory mode
if (-not $Path) {
    $Path = '.'
}

# Resolve files to scan
$resolvedPath = Resolve-Path -Path $Path -ErrorAction Stop

$filesToScan = if (Test-Path $resolvedPath.Path -PathType Container) {
    $gciParams = @{
        Path    = $resolvedPath.Path
        Include = @('*.ps1', '*.psm1', '*.psd1')
        File    = $true
    }
    if ($Recurse) {
        $gciParams['Recurse'] = $true
    }
    Get-ChildItem @gciParams
}
else {
    Get-Item $resolvedPath.Path
}

if (-not $filesToScan) {
    Write-Host "No PowerShell files found at '$Path'." -ForegroundColor Yellow
    return
}

Write-Host "Scanning $($filesToScan.Count) file(s)..." -ForegroundColor Cyan

# Process each file
$convertParams = @{}
if ($Apply) { $convertParams['Apply'] = $true }
if ($BackupPath) { $convertParams['BackupPath'] = $BackupPath }

$results = $filesToScan | ForEach-Object {
    ConvertTo-PSResourceGetScript -Path $_.FullName @convertParams
}

if ($PassThru) {
    $results
}
else {
    $results | Format-MigrationReport
}

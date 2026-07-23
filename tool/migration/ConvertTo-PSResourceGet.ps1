# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Converts PowerShellGet v2 cmdlet usage to PSResourceGet equivalents.

.DESCRIPTION
    Scans PowerShell files (.ps1, .psm1, .psd1) or inline script strings for
    PSGet v2 cmdlet usage (e.g., Install-Module, Find-Module) and converts them
    to their PSResourceGet equivalents (e.g., Install-PSResource, Find-PSResource).

    Handles cmdlet name changes, parameter renames, version range syntax
    transformations, and removed parameters with appropriate warnings.

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

# ============================================================================
#region Cmdlet & Parameter Mapping
# ============================================================================

function Get-PSGetCommandMapping {
    [OutputType([hashtable])]
    param()

    return @{
        # Find cmdlets
        'Find-Module'           = @{ Command = 'Find-PSResource' }
        'Find-Script'           = @{ Command = 'Find-PSResource' }
        'Find-DscResource'      = @{ Command = 'Find-PSResource'; ExtraParams = @{ Type = 'DscResource' } }
        'Find-Command'          = @{ Command = 'Find-PSResource'; ExtraParams = @{ Type = 'Command' } }
        'Find-RoleCapability'   = @{ Command = $null; Warning = 'Find-RoleCapability has no PSResourceGet equivalent. Manual migration required.' }

        # Install cmdlets
        'Install-Module'        = @{ Command = 'Install-PSResource' }
        'Install-Script'        = @{ Command = 'Install-PSResource' }

        # Update cmdlets
        'Update-Module'         = @{ Command = 'Update-PSResource' }
        'Update-Script'         = @{ Command = 'Update-PSResource' }

        # Uninstall cmdlets
        'Uninstall-Module'      = @{ Command = 'Uninstall-PSResource' }
        'Uninstall-Script'      = @{ Command = 'Uninstall-PSResource' }

        # Save cmdlets
        'Save-Module'           = @{ Command = 'Save-PSResource' }
        'Save-Script'           = @{ Command = 'Save-PSResource' }

        # Publish cmdlets
        'Publish-Module'        = @{ Command = 'Publish-PSResource' }
        'Publish-Script'        = @{ Command = 'Publish-PSResource' }

        # Get installed
        'Get-InstalledModule'   = @{ Command = 'Get-InstalledPSResource' }
        'Get-InstalledScript'   = @{ Command = 'Get-InstalledPSResource' }

        # Repository cmdlets
        'Register-PSRepository'   = @{ Command = 'Register-PSResourceRepository' }
        'Unregister-PSRepository' = @{ Command = 'Unregister-PSResourceRepository' }
        'Set-PSRepository'        = @{ Command = 'Set-PSResourceRepository' }
        'Get-PSRepository'        = @{ Command = 'Get-PSResourceRepository' }

        # Script file info cmdlets
        'New-ScriptFileInfo'    = @{ Command = 'New-PSScriptFileInfo' }
        'Test-ScriptFileInfo'   = @{ Command = 'Test-PSScriptFileInfo' }
        'Update-ScriptFileInfo' = @{ Command = 'Update-PSScriptFileInfo' }

        # Module manifest
        'Update-ModuleManifest' = @{ Command = 'Update-PSModuleManifest' }
    }
}

function Get-PSGetParameterMapping {
    [OutputType([hashtable])]
    param()

    return @{
        # Simple renames
        'AllowPrerelease' = @{ Type = 'Rename'; NewName = 'Prerelease' }
        'NuGetApiKey'     = @{ Type = 'Rename'; NewName = 'ApiKey' }

        # Version parameters are handled specially (Transform type)
        'RequiredVersion' = @{ Type = 'Transform'; Handler = 'VersionExact' }
        'MinimumVersion'  = @{ Type = 'Transform'; Handler = 'VersionRange' }
        'MaximumVersion'  = @{ Type = 'Transform'; Handler = 'VersionRange' }
        'AllVersions'     = @{ Type = 'Transform'; Handler = 'VersionAll' }

        # Includes → Type for Find cmdlets
        'Includes'        = @{ Type = 'Rename'; NewName = 'Type' }

        # Removed parameters
        'AllowClobber'       = @{ Type = 'Remove'; Warning = '-AllowClobber is not needed in PSResourceGet (clobber is allowed by default).' }
        'SkipPublisherCheck' = @{ Type = 'Remove'; Warning = '-SkipPublisherCheck is not supported in PSResourceGet.' }
        'Force'              = @{ Type = 'Remove'; Warning = '-Force semantics differ in PSResourceGet. Review the migrated command. Use -Reinstall for Install-PSResource if forced reinstall is needed.' }
        'Proxy'              = @{ Type = 'Remove'; Warning = '-Proxy is not supported in PSResourceGet. Configure proxy at the system level.' }
        'ProxyCredential'    = @{ Type = 'Remove'; Warning = '-ProxyCredential is not supported in PSResourceGet. Configure proxy at the system level.' }
    }
}

#endregion

# ============================================================================
#region AST Scanner
# ============================================================================

function Find-PSGetCommand {
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $commandMapping = Get-PSGetCommandMapping
    $psGetCommands = $commandMapping.Keys

    $resolvedPath = Resolve-Path -Path $Path -ErrorAction Stop
    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        $resolvedPath.Path, [ref]$tokens, [ref]$parseErrors
    )

    if ($parseErrors.Count -gt 0) {
        Write-Warning "Parse errors in '$Path': $(($parseErrors | ForEach-Object { $_.Message }) -join '; ')"
    }

    $commandAsts = $ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.CommandAst]
    }, $true)

    foreach ($cmdAst in $commandAsts) {
        $commandName = $cmdAst.GetCommandName()
        if ($commandName -and $psGetCommands -contains $commandName) {
            [PSCustomObject]@{
                CommandName = $commandName
                Line        = $cmdAst.Extent.StartLineNumber
                Column      = $cmdAst.Extent.StartColumnNumber
                StartOffset = $cmdAst.Extent.StartOffset
                EndOffset   = $cmdAst.Extent.EndOffset
                ExtentText  = $cmdAst.Extent.Text
                CommandAst  = $cmdAst
            }
        }
    }
}

#endregion

# ============================================================================
#region Command Converter
# ============================================================================

function Convert-PSGetCommand {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject] $CommandInfo
    )

    $commandMapping = Get-PSGetCommandMapping
    $parameterMapping = Get-PSGetParameterMapping

    $mapping = $commandMapping[$CommandInfo.CommandName]
    $warnings = [System.Collections.Generic.List[string]]::new()

    if ($null -eq $mapping.Command) {
        return [PSCustomObject]@{
            OriginalText  = $CommandInfo.ExtentText
            ConvertedText = $null
            Warnings      = @($mapping.Warning)
            Line          = $CommandInfo.Line
            Column        = $CommandInfo.Column
            Status        = 'NoEquivalent'
        }
    }

    $cmdAst = $CommandInfo.CommandAst
    $elements = $cmdAst.CommandElements

    $newCmdName = $mapping.Command
    $newParams = [System.Collections.Generic.List[string]]::new()

    # Track version-related parameters for merging
    $minimumVersion = $null
    $maximumVersion = $null
    $hasRequiredVersion = $false
    $hasAllVersions = $false
    $hasForce = $false

    $extraParams = if ($mapping.ContainsKey('ExtraParams')) { $mapping.ExtraParams } else { @{} }

    # Parse existing parameters from AST
    $i = 1  # skip first element (command name)
    while ($i -lt $elements.Count) {
        $element = $elements[$i]

        if ($element -is [System.Management.Automation.Language.CommandParameterAst]) {
            $paramName = $element.ParameterName
            $paramValue = $null

            # Known switch parameters that never take a value argument
            $switchParams = @(
                'Force', 'AllowClobber', 'AllowPrerelease', 'AllVersions',
                'SkipPublisherCheck', 'Reinstall', 'Prerelease',
                'AcceptLicense', 'NoClobber', 'NoPathUpdate',
                'PassThru', 'WhatIf', 'Confirm', 'TrustRepository'
            )

            if ($null -ne $element.Argument) {
                $paramValue = $element.Argument.Extent.Text
            }
            elseif ($switchParams -notcontains $paramName -and
                    ($i + 1) -lt $elements.Count -and
                    $elements[$i + 1] -isnot [System.Management.Automation.Language.CommandParameterAst]) {
                $i++
                $paramValue = $elements[$i].Extent.Text
            }

            # Apply parameter mapping
            $paramRule = $null
            foreach ($key in $parameterMapping.Keys) {
                if ($key -eq $paramName) {
                    $paramRule = $parameterMapping[$key]
                    break
                }
            }

            if ($null -ne $paramRule) {
                switch ($paramRule.Type) {
                    'Rename' {
                        if ($null -ne $paramValue) {
                            if ($extraParams.ContainsKey($paramRule.NewName)) {
                                $warnings.Add("Parameter '-$paramName' conflicts with auto-added '-$($paramRule.NewName)' from cmdlet mapping. Using the explicit value.")
                                $extraParams.Remove($paramRule.NewName)
                            }
                            $newParams.Add("-$($paramRule.NewName) $paramValue")
                        }
                        else {
                            $newParams.Add("-$($paramRule.NewName)")
                        }
                    }
                    'Remove' {
                        $warnings.Add($paramRule.Warning)
                        if ($paramName -eq 'Force') {
                            $hasForce = $true
                        }
                    }
                    'Transform' {
                        switch ($paramRule.Handler) {
                            'VersionExact' {
                                $hasRequiredVersion = $true
                                if ($null -ne $paramValue) {
                                    $newParams.Add("-Version $paramValue")
                                }
                            }
                            'VersionRange' {
                                if ($paramName -eq 'MinimumVersion') {
                                    $minimumVersion = $paramValue
                                }
                                elseif ($paramName -eq 'MaximumVersion') {
                                    $maximumVersion = $paramValue
                                }
                            }
                            'VersionAll' {
                                $hasAllVersions = $true
                            }
                        }
                    }
                }
            }
            else {
                # Unknown/unmapped parameter — pass through as-is
                if ($null -ne $paramValue) {
                    $newParams.Add("-$paramName $paramValue")
                }
                else {
                    $newParams.Add("-$paramName")
                }
            }
        }
        else {
            # Positional argument — pass through
            $newParams.Add($element.Extent.Text)
        }

        $i++
    }

    # Handle version range merging
    if (-not $hasRequiredVersion -and -not $hasAllVersions) {
        if ($null -ne $minimumVersion -and $null -ne $maximumVersion) {
            $minVal = $minimumVersion.Trim("'`"")
            $maxVal = $maximumVersion.Trim("'`"")
            # Use double quotes if the value contains a variable reference
            $quote = if ($minVal -match '\$' -or $maxVal -match '\$') { '"' } else { "'" }
            $newParams.Add("-Version $quote[$minVal,$maxVal]$quote")
        }
        elseif ($null -ne $minimumVersion) {
            $minVal = $minimumVersion.Trim("'`"")
            $quote = if ($minVal -match '\$') { '"' } else { "'" }
            $newParams.Add("-Version $quote[$minVal,)$quote")
        }
        elseif ($null -ne $maximumVersion) {
            $maxVal = $maximumVersion.Trim("'`"")
            $quote = if ($maxVal -match '\$') { '"' } else { "'" }
            $newParams.Add("-Version $quote(,$maxVal]$quote")
        }
    }

    if ($hasAllVersions) {
        $newParams.Add("-Version '*'")
    }

    # Add -Reinstall if -Force was used with Install-PSResource
    if ($hasForce -and $newCmdName -eq 'Install-PSResource') {
        $newParams.Add('-Reinstall')
        $warnings.Add("Replaced '-Force' with '-Reinstall' for Install-PSResource.")
    }

    # Add extra params from cmdlet mapping (e.g., -Type DscResource)
    foreach ($extraKey in $extraParams.Keys) {
        $newParams.Add("-$extraKey $($extraParams[$extraKey])")
    }

    $convertedParts = @($newCmdName) + $newParams
    $convertedText = $convertedParts -join ' '

    return [PSCustomObject]@{
        OriginalText  = $CommandInfo.ExtentText
        ConvertedText = $convertedText
        Warnings      = $warnings.ToArray()
        Line          = $CommandInfo.Line
        Column        = $CommandInfo.Column
        Status        = 'Converted'
    }
}

#endregion

# ============================================================================
#region File-Level Orchestrator
# ============================================================================

function ConvertTo-PSResourceGetScript {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, ValueFromPipeline, ValueFromPipelineByPropertyName)]
        [Alias('FullName')]
        [string] $Path,

        [switch] $Apply,

        [string] $BackupPath
    )

    process {
        $resolvedPath = Resolve-Path -Path $Path -ErrorAction Stop
        $filePath = $resolvedPath.Path

        Write-Verbose "Scanning: $filePath"

        $commands = @(Find-PSGetCommand -Path $filePath)

        if ($commands.Count -eq 0) {
            Write-Verbose "No PSGet v2 commands found in '$filePath'."
            return
        }

        $conversions = foreach ($cmd in $commands) {
            $result = Convert-PSGetCommand -CommandInfo $cmd
            $result | Add-Member -NotePropertyName 'File' -NotePropertyValue $filePath
            $result | Add-Member -NotePropertyName 'StartOffset' -NotePropertyValue $cmd.StartOffset
            $result | Add-Member -NotePropertyName 'EndOffset' -NotePropertyValue $cmd.EndOffset
            $result
        }

        # Output the conversion results
        $conversions | ForEach-Object {
            [PSCustomObject]@{
                File          = $_.File
                Line          = $_.Line
                Column        = $_.Column
                OriginalText  = $_.OriginalText
                ConvertedText = $_.ConvertedText
                Warnings      = $_.Warnings
                Status        = $_.Status
            }
        }

        # Apply changes if requested
        if ($Apply -and $PSCmdlet.ShouldProcess($filePath, 'Convert PSGet commands to PSResourceGet')) {
            $content = [System.IO.File]::ReadAllText($filePath)

            if ($BackupPath) {
                $backupDir = $BackupPath
                if (-not (Test-Path $backupDir)) {
                    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
                }
                $backupFile = Join-Path $backupDir ((Split-Path $filePath -Leaf) + '.bak')
            }
            else {
                $backupFile = "$filePath.bak"
            }
            Copy-Item -Path $filePath -Destination $backupFile -Force
            Write-Verbose "Backup created: $backupFile"

            $sortedConversions = $conversions |
                Where-Object { $_.Status -eq 'Converted' -and $null -ne $_.ConvertedText } |
                Sort-Object -Property StartOffset -Descending

            foreach ($conv in $sortedConversions) {
                $content = $content.Substring(0, $conv.StartOffset) +
                           $conv.ConvertedText +
                           $content.Substring($conv.EndOffset)
            }

            [System.IO.File]::WriteAllText($filePath, $content)
            Write-Verbose "File updated: $filePath"
        }
    }
}

#endregion

# ============================================================================
#region String Converter
# ============================================================================

function ConvertTo-PSResourceGetString {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string] $InputScript
    )

    process {
        $tempFile = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.ps1'
        try {
            [System.IO.File]::WriteAllText($tempFile, $InputScript)

            $commands = @(Find-PSGetCommand -Path $tempFile)
            $allWarnings = [System.Collections.Generic.List[string]]::new()

            if ($commands.Count -eq 0) {
                return [PSCustomObject]@{
                    ConvertedScript = $InputScript
                    Conversions     = @()
                    Warnings        = @()
                }
            }

            $conversions = foreach ($cmd in $commands) {
                $result = Convert-PSGetCommand -CommandInfo $cmd
                $result | Add-Member -NotePropertyName 'StartOffset' -NotePropertyValue $cmd.StartOffset
                $result | Add-Member -NotePropertyName 'EndOffset' -NotePropertyValue $cmd.EndOffset
                foreach ($w in $result.Warnings) { $allWarnings.Add($w) }
                $result
            }

            $output = $InputScript
            $sortedConversions = $conversions |
                Where-Object { $_.Status -eq 'Converted' -and $null -ne $_.ConvertedText } |
                Sort-Object -Property StartOffset -Descending

            foreach ($conv in $sortedConversions) {
                $output = $output.Substring(0, $conv.StartOffset) +
                          $conv.ConvertedText +
                          $output.Substring($conv.EndOffset)
            }

            return [PSCustomObject]@{
                ConvertedScript = $output
                Conversions     = @($conversions | ForEach-Object {
                    [PSCustomObject]@{
                        Line          = $_.Line
                        OriginalText  = $_.OriginalText
                        ConvertedText = $_.ConvertedText
                        Warnings      = $_.Warnings
                        Status        = $_.Status
                    }
                })
                Warnings        = $allWarnings.ToArray()
            }
        }
        finally {
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        }
    }
}

#endregion

# ============================================================================
#region Report Formatter
# ============================================================================

function Format-MigrationReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [PSCustomObject[]] $Results
    )

    begin {
        $allResults = [System.Collections.Generic.List[PSCustomObject]]::new()
    }

    process {
        foreach ($r in $Results) {
            $allResults.Add($r)
        }
    }

    end {
        if ($allResults.Count -eq 0) {
            Write-Host "`n  No PSGet v2 commands found. Nothing to migrate.`n" -ForegroundColor Green
            return
        }

        $grouped = $allResults | Group-Object -Property File

        Write-Host "`n========================================" -ForegroundColor Cyan
        Write-Host "  PSGet → PSResourceGet Migration Report" -ForegroundColor Cyan
        Write-Host "========================================`n" -ForegroundColor Cyan

        foreach ($group in $grouped) {
            Write-Host "  File: $($group.Name)" -ForegroundColor Yellow
            Write-Host "  $('-' * 60)" -ForegroundColor DarkGray

            foreach ($item in $group.Group) {
                $statusColor = switch ($item.Status) {
                    'Converted'     { 'Green' }
                    'NoEquivalent'  { 'Red' }
                    default         { 'White' }
                }

                Write-Host "    Line $($item.Line):" -ForegroundColor White
                Write-Host "      - $($item.OriginalText)" -ForegroundColor Red
                if ($item.ConvertedText) {
                    Write-Host "      + $($item.ConvertedText)" -ForegroundColor Green
                }

                foreach ($w in $item.Warnings) {
                    Write-Host "      ⚠ $w" -ForegroundColor DarkYellow
                }
                Write-Host ""
            }
        }

        $total = $allResults.Count
        $converted = ($allResults | Where-Object Status -eq 'Converted').Count
        $noEquiv = ($allResults | Where-Object Status -eq 'NoEquivalent').Count
        $withWarnings = ($allResults | Where-Object { $_.Warnings.Count -gt 0 }).Count

        Write-Host "  Summary" -ForegroundColor Cyan
        Write-Host "  $('-' * 60)" -ForegroundColor DarkGray
        Write-Host "    Total commands found : $total"
        Write-Host "    Converted            : $converted" -ForegroundColor Green
        Write-Host "    No equivalent        : $noEquiv" -ForegroundColor Red
        Write-Host "    With warnings        : $withWarnings" -ForegroundColor DarkYellow
        Write-Host ""
    }
}

#endregion

# ============================================================================
#region Main Entry Point — only runs when script is invoked directly, not dot-sourced
# ============================================================================

if ($MyInvocation.InvocationName -ne '.') {

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

} # end if not dot-sourced

#endregion

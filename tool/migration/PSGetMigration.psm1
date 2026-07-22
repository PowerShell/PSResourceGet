# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Migration tool to convert PowerShellGet v2 (PSGet) cmdlet usage to PSResourceGet equivalents.

.DESCRIPTION
    This module provides functions to scan PowerShell script files for PSGet v2 cmdlet usage
    and convert them to their PSResourceGet equivalents, handling cmdlet name changes,
    parameter renames, and version range syntax transformations.
#>

#region Cmdlet Mapping

function Get-PSGetCommandMapping {
    <#
    .SYNOPSIS
        Returns a hashtable mapping PSGet v2 cmdlet names to PSResourceGet equivalents.
    #>
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
    <#
    .SYNOPSIS
        Returns parameter transformation rules for PSGet v2 to PSResourceGet migration.
    .DESCRIPTION
        Each entry defines how a PSGet v2 parameter maps to its PSResourceGet equivalent.
        Mapping types:
          - Rename: parameter name changes but value stays the same.
          - Remove: parameter is dropped (with optional warning).
          - Transform: parameter requires value/logic transformation (handled by Convert-PSGetCommand).
    #>
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

#region AST Scanner

function Find-PSGetCommand {
    <#
    .SYNOPSIS
        Finds all PSGet v2 cmdlet invocations in a PowerShell file using AST parsing.
    .PARAMETER Path
        Path to the PowerShell file to scan.
    .OUTPUTS
        Objects with properties: CommandName, Line, Column, Extent, CommandAst
    #>
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
        Write-Warning "Parse errors in '$Path': $($parseErrors | ForEach-Object { $_.Message } | Join-String -Separator '; ')"
    }

    # Find all CommandAst nodes
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

#region Command Converter

function Convert-PSGetCommand {
    <#
    .SYNOPSIS
        Converts a single PSGet v2 command invocation to its PSResourceGet equivalent.
    .PARAMETER CommandInfo
        A PSCustomObject from Find-PSGetCommand containing the AST node and metadata.
    .OUTPUTS
        PSCustomObject with: OriginalText, ConvertedText, Warnings, Line
    #>
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

    # If no equivalent exists, return a warning
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

    # Build the new command parts
    $newCmdName = $mapping.Command
    $newParams = [System.Collections.Generic.List[string]]::new()

    # Track version-related parameters for merging
    $minimumVersion = $null
    $maximumVersion = $null
    $hasRequiredVersion = $false
    $hasAllVersions = $false
    $hasForce = $false

    # Extra parameters from the mapping (e.g., -Type DscResource for Find-DscResource)
    $extraParams = if ($mapping.ContainsKey('ExtraParams')) { $mapping.ExtraParams } else { @{} }

    # Parse existing parameters from AST
    $i = 1  # skip first element (command name)
    while ($i -lt $elements.Count) {
        $element = $elements[$i]

        if ($element -is [System.Management.Automation.Language.CommandParameterAst]) {
            $paramName = $element.ParameterName
            $paramValue = $null

            # Check if the parameter has an argument (inline via : or next element)
            if ($null -ne $element.Argument) {
                $paramValue = $element.Argument.Extent.Text
            }
            elseif (($i + 1) -lt $elements.Count -and
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
                            # If the extra params would set the same parameter, check for conflict
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
            $newParams.Add("-Version '[$minVal,$maxVal]'")
        }
        elseif ($null -ne $minimumVersion) {
            $minVal = $minimumVersion.Trim("'`"")
            $newParams.Add("-Version '[$minVal,)'")
        }
        elseif ($null -ne $maximumVersion) {
            $maxVal = $maximumVersion.Trim("'`"")
            $newParams.Add("-Version '(,$maxVal]'")
        }
    }

    # Handle -AllVersions → -Version '*'
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

    # Compose final command
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

#region File-Level Orchestrator

function ConvertTo-PSResourceGetScript {
    <#
    .SYNOPSIS
        Scans a PowerShell file for PSGet v2 usage and returns migration results.
    .PARAMETER Path
        Path to the PowerShell file to process.
    .PARAMETER Apply
        If specified, modifies the file in-place with the converted commands.
    .PARAMETER BackupPath
        Directory to store backup copies before modification. Defaults to creating .bak files alongside originals.
    .OUTPUTS
        PSCustomObject per conversion with: File, Line, OriginalText, ConvertedText, Warnings, Status
    #>
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

        # Convert each command
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

            # Create backup
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

            # Apply replacements in reverse offset order so positions stay valid
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

#region Formatting

function Format-MigrationReport {
    <#
    .SYNOPSIS
        Formats migration results into a human-readable report.
    .PARAMETER Results
        Array of conversion result objects from ConvertTo-PSResourceGetScript.
    #>
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

        # Summary
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

# Export module members
Export-ModuleMember -Function @(
    'Get-PSGetCommandMapping',
    'Get-PSGetParameterMapping',
    'Find-PSGetCommand',
    'Convert-PSGetCommand',
    'ConvertTo-PSResourceGetScript',
    'Format-MigrationReport'
)

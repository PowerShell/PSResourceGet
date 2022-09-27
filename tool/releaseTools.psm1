# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

using module PowerShellForGitHub
using namespace System.Management.Automation

$script:PullRequests = @()
$script:BugFixes = @()
$script:NewFeatures = @()
$script:Repo = Get-GitHubRepository -OwnerName PowerShell -RepositoryName PowerShellGet
$script:Path = (Get-Item $PSScriptRoot).Parent.FullName
$script:ChangelogFile = "$Path/CHANGELOG.md"

<#
.SYNOPSIS
    Creates and checks out the `release` branch if not already existing
#>
function Update-Branch {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    $Branch = git branch --show-current
    if ($Branch -ne "release") {
        if ($PSCmdlet.ShouldProcess("release", "git checkout -B")) {
            git checkout -B "release"
        }
    }
}

<#
.SYNOPSIS
    Formats the pull requests into bullet points
#>
function Get-Bullet {
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [PSCustomObject]$PullRequest
    )
    ("-", $PullRequest.title, ("(#" + $PullRequest.PullRequestNumber + ")") -join " ").Trim() 
}

<#
.SYNOPSIS
  Gets and categorizes the CHANGELOG content with PRs merged since the last release.
.DESCRIPTION
  Uses the local Git repositories but does not pull, so ensure HEAD is where you
  want it.
#>
function Get-Changelog {  
    # This will take some time because it has to pull all PRs and then filter
    $script:PullRequests = $script:Repo | Get-GitHubPullRequest -State 'closed' |
        Where-Object { $_.labels.LabelName -match 'Release' } |
        Where-Object { -not $_.title.StartsWith("[WIP]") } | 
        Where-Object { -not $_.title.StartsWith("WIP") } 

    $PullRequests | ForEach-Object {
        if ($_.labels.LabelName -match 'PR-Bug') {
            $script:BugFixes += Get-Bullet($_)
        }
        else {
            $script:NewFeatures += Get-Bullet($_)
        }
    }
}

<#
.SYNOPSIS
  Creates the CHANGELOG content
#>
function Set-Changelog {
    param(
        [Parameter(Mandatory)]
        [string]$Version
    )
    @(
        "## $Version"
        ""
        "### New Features"
        $script:NewFeatures
        ""
        "### Bug Fixes"
        $script:BugFixes
        ""
    )
}

<#
.SYNOPSIS
  Updates the CHANGELOG file
#>
function Update-Changelog {
    param(
        [Parameter(Mandatory)]
        [string]$Version
    )
    Get-Changelog

    $CurrentChangeLog = Get-Content -Path $script:ChangelogFile
    @(
        $CurrentChangeLog[0..1]
        Set-Changelog $Version
        $CurrentChangeLog[2..$CurrentChangeLog.Length]
    ) | Set-Content -Encoding utf8NoBOM -Path $script:ChangelogFile
}

<#
.SYNOPSIS
    Updates the PowerShellGet.psd1 file
# TODO: Update ModuleVersion and Prerelease once the format is agreed upon
#>
function Update-PSDFile {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$Version
    )
    $CurrentPSDFile = Get-Content -Path ".\src\PowerShellGet.psd1"
    $Header = $CurrentPSDFile.Where({$_.StartsWith("## ")}, "First")

    @(
        $CurrentPSDFile.Where({ $_ -eq $Header }, "Until")
        Set-Changelog $Version
        $CurrentPSDFile.Where({ $_ -eq $Header }, "SkipUntil")
    ) | Set-Content -Encoding utf8NoBOM -Path ".\src\PowerShellGet.psd1"

    if ($PSCmdlet.ShouldProcess(".\src\PowerShellGet.psd1", "git add")) {
        git add "src\PowerShellGet.psd1"
    }
}

<#
.SYNOPSIS
    Creates a draft GitHub PR to update the CHANGELOG.md file
#>
function New-ReleasePR {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$Version,

        [Parameter(Mandatory)]
        [string]$Username
    )

    Update-Branch
    if ($PSCmdlet.ShouldProcess("$script:ChangelogFile", "git commit")) {
        git add $ChangelogFile
        git commit -m "Update CHANGELOG for ``$Version``"
    }

    if ($PSCmdlet.ShouldProcess("release", "git push")) {
        Write-Host "Pushing release branch..."
        git push --force-with-lease origin release
    }

    $Params = @{
        Head = "$($Username):release"
        Base = "master"
        Draft = $true
        Title = "Update CHANGELOG for ``$Version``"
        Body = "An automated PR to update the CHANGELOG.md file for a new release"
    }

    $PR = $script:Repo | New-GitHubPullRequest @Params
    Write-Host "Draft PR URL: $($PR.html_url)"
}

<#
.SYNOPSIS
    Given the version and the username for the forked repository, updates the CHANGELOG.md file and creates a draft GitHub PR 
#>
function New-Release {
    param(
        [Parameter(Mandatory)]
        [string]$Version,

        [Parameter(Mandatory)]
        [string]$Username
    )
    Update-Changelog $Version
    New-ReleasePR -Version $Version -Username $Username
}

<#
.SYNOPSIS
    Removes the `Release` label after updating the CHANGELOG.md file 
#>
function Remove-Release-Label {
    $script:PullRequests = $script:Repo | Get-GitHubPullRequest -State 'closed' |
        Where-Object { $_.labels.LabelName -match 'Release' } |
        Where-Object { -not $_.title.StartsWith("[WIP]") } | 
        Where-Object { -not $_.title.StartsWith("WIP") }

    $script:PullRequests | ForEach-Object {
        $script:Repo | Remove-GitHubIssueLabel -Label Release -Issue $_.PullRequestNumber
    }
}
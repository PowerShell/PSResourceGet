# requires -Version 6.0
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

using module PowerShellForGitHub
using namespace System.Management.Automation

$PullRequests = @()
$Repo = Get-GitHubRepository -OwnerName PowerShell -RepositoryName PowerShellGet
$Path = (Get-Item $PSScriptRoot).Parent.FullName
$ChangelogFile = "$Path/CHANGELOG.md"

function Update-Branch {
    [CmdletBinding(SupportsShouldProcess)]
    $Branch = git branch --show-current
    if ($Branch -ne "release") {
        if ($PSCmdlet.ShouldProcess("release", "git checkout -B")) {
            git checkout -B "release"
        }
    }
}

function Get-Bullet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [PSCustomObject]$PullRequest
    )

    ("-", $PullRequest.title, ("(#" + $PullRequest.PullRequestNumber + ")") -join " ").Trim() 
}

<#
.SYNOPSIS
  Gets the unpublished content from the changelog.
.DESCRIPTION
  This is used so that we can manually touch-up the automatically updated
  changelog, and then bring its contents into the extension's changelog or
  the GitHub release. It just gets the first header's contents.
#>
function Get-FirstChangelog {
    $Changelog = Get-Content -Path $ChangelogFile
    
    # NOTE: The space after the header marker is important! Otherwise ### matches.
    $Header = $Changelog.Where({$_.StartsWith("## ")}, "First")
    $Changelog.Where(
        { $_ -eq $Header }, "SkipUntil"
    ).Where(
        { $_.StartsWith("## ") -and $_ -ne $Header }, "Until"
    )
}

<#
.SYNOPSIS
  Gets current version from changelog as `[semver]`.
#>
function Get-Version {
    $Version = (Get-FirstChangelog)[0]
    if ($Version -match '## (?<version>\d+\.\d+\.\d+(-beta\d*)?)') {
        return $Matches.version
    } else {
        Write-Error "Couldn't find version from changelog!"
    }
}

<#
.SYNOPSIS
  Updates the CHANGELOG file with PRs merged since the last release.
.DESCRIPTION
  Uses the local Git repositories but does not pull, so ensure HEAD is where you
  want it. Creates the branch `release` if not already checked out. Handles any
  merge option for PRs, but is a little slow as it queries all PRs.
#>
function Get-Changelog {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        # TODO: Validate version style for each repo.
        [Parameter(Mandatory)]
        [string]$Version
    )

    $BugFixes = @()
    $NewFeatures = @()
    # This will take some time because it has to pull all PRs and then filter

    $PullRequests = $Repo | Get-GitHubPullRequest -State 'closed' |
    Where-Object { $_.labels.LabelName -match 'Release' } |
    Where-Object { -not $_.title.StartsWith("[WIP]") } | 
    Where-Object { -not $_.title.StartsWith("WIP") } 

    $PullRequests | ForEach-Object {
        if ($_.labels.LabelName -match 'PR-Bug') {
            $BugFixes += Get-Bullet($_)
        }
        else {
            $NewFeatures += Get-Bullet($_)
        }
    }

    @(
        "## $Version"
        ""
        "### New Features"
        $NewFeatures
        ""
        "### Bug Fixes"
        $BugFixes
        ""
    )
}

function Update-Changelog {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        # TODO: Validate version style for each repo.
        [Parameter(Mandatory)]
        [string]$Version
    )

    $CurrentChangeLog = Get-Content -Path $ChangelogFile
    @(
        $CurrentChangeLog[0..1]
        Get-Changelog $Version
        $CurrentChangeLog[2..$CurrentChangeLog.Length]
    ) | Set-Content -Encoding utf8NoBOM -Path $ChangelogFile
}

function New-ReleasePR {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$Version
    )

    Update-Branch
    if ($PSCmdlet.ShouldProcess("release", "git push")) {
        Write-Host "Pushing release branch..."
        git push --force-with-lease origin release
    }

    $Repo = Get-GitHubRepository -OwnerName PowerShell -RepositoryName PowerShellGet
    Write-Host "Gotten repo $($Repo)"

    $Params = @{
        Head = "alyss1303/PowerShellGet:release"
        Base = "PowerShell/PowerShellGet:master"
        Draft = $true
        Title = "Update CHANGELOG for $($Version)"
    }

    $PR = $Repo | New-GitHubPullRequest @Params
    Write-Host "Draft PR URL: $($PR.html_url)"
}

function Remove-Release-Label {
    $PullRequests | ForEach-Object {
        $Repo | Remove-GitHubIssueLabel -Label Release -Issue $_.PullRequestNumber
    }
}

# Get all GitHub labels and filter the labels for release
$labels = Get-GitHubLabel -OwnerName PowerShell -RepositoryName PowerShellGet
$labels | Where-Object { $_.color -match 'B2F74F' } #>

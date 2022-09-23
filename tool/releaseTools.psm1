# requires -Version 6.0
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

#using module PowerShellForGitHub
using namespace System.Management.Automation

function Get-Bullet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [PSCustomObject]$PullRequest
    )

    $PullRequest | -Process { ("-", $_.title, ("(#" + $_.PullRequestNumber + ")") -join " ").Trim() }
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
    $ChangelogFile = ".\CHANGELOG.md"
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
function Update-Changelog {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        # TODO: Validate version style for each repo.
        [Parameter(Mandatory)]
        [string]$Version
    )

    $RepositoryName = 'PowerShellGet'
    $Repo = Get-GitHubRepository -OwnerName PowerShell -RepositoryName $RepositoryName

    $BugFixes = @()
    $NewFeatures = @()
    # This will take some time because it has to pull all PRs and then filter
    $PullRequests = $Repo | Get-GitHubPullRequest -State 'closed' |
        Where-Object { $_.labels.LabelName -match 'Release' } |
        Where-Object { -not $_.title.StartsWith("[WIP]") } | 
        Where-Object { -not $_.title.StartsWith("WIP") } |
        if ($_.labels.LabelName -match 'PR-Bug') {
            $BugFixes += Get-Bullet($_)
        }
        else {
            $NewFeatures += Get-Bullet($_)
        }
        
    $NewSection = switch ($PullRequests.labels.LabelName) {
        'PR-Bug' {

        }
    }
}



$allIssues = $Repo | Get-GitHubIssue -State 'closed'
$closedIssues = $allIssues | Where-Object { $_.pull_request -eq $null }
$labelledIssues = $closedIssues | Where-Object { $_.labels.LabelName -match 'Issue-Bug|feature_request' }

# Get all GitHub labels and filter the labels for release
$labels = Get-GitHubLabel -OwnerName PowerShell -RepositoryName PowerShellGet
$labels | Where-Object { $_.color -match 'B2F74F' }

# Remove a label from a pull request
Remove-GitHubIssueLabel -OwnerName PowerShell -RepositoryName PowerShellGet -Label Release -Issue 806

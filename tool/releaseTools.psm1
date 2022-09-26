# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

using module PowerShellForGitHub
using namespace System.Management.Automation

$PullRequests = @()
$BugFixes = @()
$NewFeatures = @()
$Repo = Get-GitHubRepository -OwnerName PowerShell -RepositoryName PowerShellGet
$Path = (Get-Item $PSScriptRoot).Parent.FullName
$ChangelogFile = "$Path/CHANGELOG.md"

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
  Creates the CHANGELOG content with PRs merged since the last release.
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
}

function Set-Changelog {
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

<#
.SYNOPSIS
  Updates the CHANGELOG file given a version
#>
function Update-Changelog {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$Version
    )

    Get-Changelog $Version
    $CurrentChangeLog = Get-Content -Path $ChangelogFile
    @(
        $CurrentChangeLog[0..1]
        Set-Changelog $Version
        $CurrentChangeLog[2..$CurrentChangeLog.Length]
    ) | Set-Content -Encoding utf8NoBOM -Path $ChangelogFile

    if ($PSCmdlet.ShouldProcess("$ChangelogFile", "git commit")) {
        git add $ChangelogFile
    }
}

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

    if ($PSCmdlet.ShouldProcess(".\src\PowerShellGet.psd1", "git commit")) {
        git add "src\PowerShellGet.psd1"
    }
}

function New-ReleasePR {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$Version,

        [Parameter(Mandatory)]
        [string]$Username
    )

    Update-Branch
    if ($PSCmdlet.ShouldProcess("release", "git push")) {
        Write-Host "Pushing release branch..."
        git push --force-with-lease origin release
    }

    $PRTemplate = Get-Content -Path ".\.github\PULL_REQUEST_TEMPLATE.md"

    $Params = @{
        Head = "``$Username``:release"
        Base = "master"
        Draft = $true
        Title = "Update CHANGELOG for ``$Version``"
        Body = @($PRTemplate[0..4]
                "Updates CHANGELOG.md file"
                $PRTemplate[5..$PRTemplate.Length])
    }

    $PR = $Repo | New-GitHubPullRequest @Params
    Write-Host "Draft PR URL: $($PR.html_url)"
}

function Remove-Release-Label {
    $PullRequests | ForEach-Object {
        $Repo | Remove-GitHubIssueLabel -Label Release -Issue $_.PullRequestNumber
    }
}
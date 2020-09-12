$projectPath = $PSScriptRoot
$outputPath = Join-Path -Path $projectPath -ChildPath 'src' -AdditionalChildPath 'out'

function Start-Build {
    [CmdletBinding()]
    param(
        [ValidateSet("netstandard2.0")]
        [string]$Framework = "netstandard2.0",
    
        [ValidateSet("Debug", "Release")]
        [string]$Configuration = "Debug",
    
        [switch]$Clean
    )

    & (Join-Path -Path $projectPath -ChildPath 'src' -AdditionalChildPath 'build.ps1') @PSBoundParameters
}

function Start-Test {
    [CmdletBinding()]
    param()

    $modulePath = Join-Path -Path $outputPath -ChildPath 'PowerShellGet'

    $script = [scriptblock]::Create(@"
        Import-Module "$modulePath"
        Push-Location "$projectPath"
        Invoke-Pester
"@)

    # Start a new pwsh instance so that newly built PowerShellGet.dll can be loaded in process
    pwsh -noprofile -command $script

    if ($null -ne (Get-Command powershell -ErrorAction Ignore)) {
        powershell -noprofile -command $script
    }
}
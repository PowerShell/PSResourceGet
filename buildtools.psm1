# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

$ConfigurationFileName = 'package.config.json'
Import-Module -Name Microsoft.PowerShell.PSResourceGet -MinimumVersion 0.9.0

function Get-BuildConfiguration {
    [CmdletBinding()]
    param (
        [Parameter()]
        [string] $ConfigPath = '.'
    )

    $resolvedPath = Resolve-Path $ConfigPath

    if (Test-Path $resolvedPath -PathType Container) {
        $fileNamePath = Join-Path -Path $resolvedPath -ChildPath $ConfigurationFileName
    }
    else {
        $fileName = Split-Path -Path $resolvedPath -Leaf
        if ($fileName -ne $ConfigurationFileName) {
            throw "$ConfigurationFileName not found in provided pathname: $resolvedPath"
        }
        $fileNamePath = $resolvedPath
    }

    if (! (Test-Path -Path $fileNamePath -PathType Leaf)) {
        throw "$ConfigurationFileName not found at path: $resolvedPath"
    }

    $configObj = Get-Content -Path $fileNamePath | ConvertFrom-Json

    # Expand config paths to full paths
    $projectRoot = Split-Path $fileNamePath
    $configObj.SourcePath = Join-Path $projectRoot -ChildPath $configObj.SourcePath
    $configObj.TestPath = Join-Path $projectRoot -ChildPath $configObj.TestPath
    $configObj.BuildOutputPath = Join-Path $projectRoot -ChildPath $configObj.BuildOutputPath
    if ($configObj.SignedOutputPath) {
        $configObj.SignedOutputPath = Join-Path $projectRoot -ChildPath $configObj.SignedOutputPath
    }
    else {
        $configObj | Add-Member -MemberType NoteProperty -Name SignedOutputPath -Value (Join-Path $projectRoot -ChildPath 'signed')
    }

    return $configObj
}

function Invoke-ModuleBuild {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [ScriptBlock] $BuildScript
    )

    Write-Verbose -Verbose -Message "Invoking build script"

    $BuildScript.Invoke()

    Write-Verbose -Verbose -Message "Finished invoking build script"
}

function Publish-ModulePackage
{
    [CmdletBinding()]
    param (
        [Parameter()]
        [Switch] $Signed
    )

    Write-Verbose -Verbose -Message "Creating new local package repo"
    $localRepoName = 'packagebuild-local-repo'
    $localRepoLocation = Join-Path -Path ([System.io.path]::GetTempPath()) -ChildPath $localRepoName
    if (Test-Path -Path $localRepoLocation) {
        Remove-Item -Path $localRepoLocation -Recurse -Force -ErrorAction Ignore
    }
    $null = New-Item -Path $localRepoLocation -ItemType Directory -Force

    Write-Verbose -Verbose -Message "Registering local package repo: $localRepoName"
    Register-PSResourceRepository -Name $localRepoName -Uri $localRepoLocation -Trusted -Force

    Write-Verbose -Verbose -Message "Publishing package to local repo: $localRepoName"
    $config = Get-BuildConfiguration
    if (! $Signed.IsPresent) {
        $modulePath = Join-Path -Path $config.BuildOutputPath -ChildPath $config.ModuleName
    } else {
        $modulePath = Join-Path -Path $config.SignedOutputPath -ChildPath $config.ModuleName
    }
    Publish-PSResource -Path $modulePath -Repository $localRepoName -SkipDependenciesCheck -Confirm:$false -Verbose

    if ($env:TF_BUILD) {
        Write-Verbose -Verbose -Message "Uploading module nuget package artifact to AzDevOps"
        $artifactName = "nupkg"
        $artifactPath = (Get-ChildItem -Path $localRepoLocation -Filter "$($config.ModuleName)*.nupkg").FullName
        $artifactPath = Resolve-Path -Path $artifactPath
        Write-Host "##vso[artifact.upload containerfolder=$artifactName;artifactname=$artifactName;]$artifactPath"
    }

    Write-Verbose -Verbose -Message "Unregistering local package repo: $localRepoName"
    Unregister-PSResourceRepository -Name $localRepoName -Confirm:$false
}

function Install-ModulePackageForTest {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [string] $PackagePath
    )

    $config = Get-BuildConfiguration

    $localRepoName = 'packagebuild-local-repo'
    $packagePathWithNupkg = Join-Path -Path $PackagePath -ChildPath "nupkg"
    Write-Verbose -Verbose -Message "Registering local package repo: $localRepoName to path: $packagePathWithNupkg"
    Register-PSResourceRepository -Name $localRepoName -Uri $packagePathWithNupkg -Trusted -Force

    $installationPath = $config.BuildOutputPath
    if ( !(Test-Path $installationPath)) {
        Write-Verbose -Verbose -Message "Creating module directory location for tests: $installationPath"
        $null = New-Item -Path $installationPath -ItemType Directory -Verbose
    }

    Write-Verbose -Verbose -Message "Installing module $($config.ModuleName) to build output path $installationPath"
    Save-PSResource -Name $config.ModuleName -Repository $localRepoName -Path $installationPath -SkipDependencyCheck -Prerelease -Confirm:$false -TrustRepository

    Write-Verbose -Verbose -Message "Unregistering local package repo: $localRepoName"
    Unregister-PSResourceRepository -Name $localRepoName -Confirm:$false
}

function Invoke-ModuleTestsACR {
    [CmdletBinding()]
    param (
        [ValidateSet("Functional", "StaticAnalysis")]
        [string[]] $Type = "Functional"
    )

    $acrTestFiles = @(
        "FindPSResourceTests/FindPSResourceContainerRegistryServer.Tests.ps1",
        "InstallPSResourceTests/InstallPSResourceContainerRegistryServer.Tests.ps1",
        "PublishPSResourceTests/PublishPSResourceContainerRegistryServer.Tests.ps1"
    )

    Invoke-ModuleTests -Type $Type -TestFilePath $acrTestFiles
}


function Invoke-ModuleTests {
    [CmdletBinding()]
    param (
       [ValidateSet("Functional", "StaticAnalysis")]
       [string[]] $Type = "Functional",
       [string[]] $TestFilePath = "."
    )

    Write-Verbose -Verbose -Message "Starting module Pester tests..."

    # Run module Pester tests.
    $config = Get-BuildConfiguration
    $tags = 'CI'
    $excludeTag = 'ManualValidationOnly'
    $testResultFileName = 'result.pester.xml'
    $testPath = $config.TestPath
    Write-Verbose -Verbose $config.ModuleName
    $moduleToTest = Join-Path -Path $config.BuildOutputPath -ChildPath "Microsoft.PowerShell.PSResourceGet"

    if ($TestFilePath.Count -gt 1) {
        $TestFilePathJoined = $TestFilePath -join ','
    }
    else {
        $TestFilePathJoined = $TestFilePath
    }

    $command = "Import-Module -Name ${moduleToTest} -Force -Verbose; Set-Location -Path ${testPath}; Invoke-Pester -Script ${TestFilePathJoined} -OutputFile ${testResultFileName} -Tags '${tags}' -ExcludeTag '${excludeTag}'"
    $pwshExePath = (Get-Process -Id $pid).Path

    Write-Verbose -Verbose -Message "Running Pester tests with command: $command using pwsh.exe path: $pwshExePath"

    try {
        & $pwshExePath -NoProfile -NoLogo -Command $command
    }
    catch {
        Write-Error -Message "Error invoking module Pester tests."
    }

    $testResultsFilePath = Join-Path -Path $testPath -ChildPath $testResultFileName

    # Note: This is commented out temporarily as code for reporting test results via result.pester.xml is not working
    # and class it references to do so can't be found. This will be fixed later.

    # # Examine Pester test results.
    # if (! (Test-Path -Path $testResultsFilePath)) {
    #     throw "Module test result file not found: '$testResultsFilePath'"
    # }
    # $xmlDoc = [xml] (Get-Content -Path $testResultsFilePath -Raw)
    # if ([int] ($xmlDoc.'test-results'.failures) -gt 0) {
    #     $failures = [System.Xml.XmlDocumentXPathExtensions]::SelectNodes($xmlDoc."test-results", './/test-case[@result = "Failure"]')
    # }
    # else {
    #     $failures = $xmlDoc.SelectNodes('.//test-case[@result = "Failure"]')
    # }
    # foreach ($testfailure in $failures) {
    #     Show-PSPesterError -TestFailure $testfailure
    # }

    # Publish test results to AzDevOps
    if ($env:TF_BUILD) {
        Write-Verbose -Verbose -Message "Uploading test results to AzDevOps"
        $powerShellName = if ($PSVersionTable.PSEdition -eq 'Core') { 'PowerShell Core' } else { 'Windows PowerShell' }
        $TestType = 'NUnit'
        $Title = "Functional Tests -  $env:AGENT_OS - $powershellName Results"
        $ArtifactPath = (Resolve-Path -Path $testResultsFilePath).ProviderPath
        $FailTaskOnFailedTests = 'true'
        $message = "vso[results.publish type=$TestType;mergeResults=true;runTitle=$Title;publishRunAttachments=true;resultFiles=$ArtifactPath;failTaskOnFailedTests=$FailTaskOnFailedTests;]"
        Write-Host "##$message"
    }

    Write-Verbose -Verbose -Message "Module Pester tests complete."
}

function Show-PSPesterError {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [Xml.XmlElement]$testFailure
    )

    $description = $testFailure.description
    $name = $testFailure.name
    $message = $testFailure.failure.message
    $stackTrace = $testFailure.failure."stack-trace"

    $fullMsg = "`n{0}`n{1}`n{2}`n{3}`{4}" -f ("Description: " + $description), ("Name:        " + $name), "message:", $message, "stack-trace:", $stackTrace

    Write-Error $fullMsg
}

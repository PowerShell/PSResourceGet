# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

function SearchConfigFile {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $startPath = $Path

    do {
        $configPath = Join-Path $startPath 'pspackageproject.json'

        if (-not (Test-Path $configPath)) {
            $startPath = Split-Path $startPath
        }
        else {
            return $configPath
        }
    } while ($newPath -ne '')
}

function Get-ProjectConfiguration {
    param(
        [Parameter()]
        [string] $ConfigPath = "."
    )

    $resolvedPath = Resolve-Path $ConfigPath

    $foundConfigFilePath = if (Test-Path $resolvedPath -PathType Container) {
        SearchConfigFile -Path $resolvedPath
    }
    else {
        if ($resolvedPath.ToString().EndsWith('pspackageproject.json') -and (Test-Path $resolvedPath -PathType Leaf)) {
            $resolvedPath
        }
    }

    if (Test-Path $foundConfigFilePath) {
        $configObj = Get-Content -Path $foundConfigFilePath | ConvertFrom-Json

        # Populate with full paths

        $projectRoot = Split-Path $foundConfigFilePath

        $configObj.SourcePath = Join-Path $projectRoot -ChildPath $configObj.SourcePath
        $configObj.TestPath = Join-Path $projectRoot -ChildPath $configObj.TestPath
        $configObj.HelpPath = Join-Path $projectRoot -ChildPath $configObj.HelpPath
        $configObj.BuildOutputPath = Join-Path $projectRoot -ChildPath $configObj.BuildOutputPath
        if ($configObj.SignedOutputPath) {
            $configObj.SignedOutputPath = Join-Path $projectRoot -ChildPath $configObj.SignedOutputPath
        }
        else {
            $configObj | Add-Member -MemberType NoteProperty -Name SignedOutputPath -Value (Join-Path $projectRoot -ChildPath 'signed')
        }

        $configObj
    }
    else {
        throw "'pspackageproject.json' not found at: $resolvePath or any or its parent"
    }
}

function Invoke-ProjectBuild {
    [CmdletBinding()]
    param(
        [Parameter()]
        [ScriptBlock]
        $BuildScript,
        [Switch]
        $SkipPublish
    )

    Write-Verbose -Verbose -Message "Invoking build script"

    $BuildScript.Invoke()

    if (!$SkipPublish.IsPresent) {
        Invoke-ProjectPublish
    }

    Write-Verbose -Verbose -Message "Finished invoking build script"
}

function Publish-Artifact
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSAvoidUsingWriteHost", "")]
    param(
        [Parameter(Mandatory)]
        $Path,
        [string]
        $Name
    )

    if (-not (Test-Path $Path)) {
        Write-Warning "Path: $Path does not exist"
        return
    }

    $resolvedPath = (Resolve-Path -Path $Path).ProviderPath

    if(!$Name)
    {
        $artifactName = [system.io.path]::GetFileName($Path)
    }
    else
    {
        $artifactName = $Name
    }

    if ($env:TF_BUILD) {
        # In Azure DevOps
        Write-Host "##vso[artifact.upload containerfolder=$artifactName;artifactname=$artifactName;]$resolvedPath"
    }
}

function Convert-ToUri ( [string]$location ) {
    $locationAsUri = $location -as [System.Uri]
    if ( $locationAsUri.Scheme ) {
        return $locationAsUri
    }
    # now determine if the path exists and is a directory
    # if it exists, return it as a file uri
    if ( Test-Path -PathType Container -LiteralPath $location ) {
        $locationAsUri = "file://${location}" -as [System.Uri]
        if( $locationAsUri.Scheme ) {
            return $locationAsUri
        }
    }
    throw "Cannot convert '$location' to System.Uri"
}

function New-ProjectPackage
{
    [CmdletBinding()]
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSUseShouldProcessForStateChangingFunctions","")]
    param (
        [Switch]
        $Signed,
        [Switch]
        $AllowPreReleaseDependencies
    )

    Write-Verbose -Message "Starting New-ProjectPackage" -Verbose
    $config = Get-ProjectConfiguration
    if(!$Signed.IsPresent)
    {
        $modulePath = Join-Path -Path $config.BuildOutputPath -ChildPath $config.ModuleName
    }
    else
    {
        $modulePath = Join-Path -Path $config.SignedOutputPath -ChildPath $config.ModuleName
    }

    $sourceName = 'pspackageproject-local-repo'
    $packageLocation = Join-Path -Path ([System.io.path]::GetTempPath()) -ChildPath $sourceName
    $modulesLocation = Join-Path -Path $packageLocation -ChildPath 'modules'

    if (Test-Path $modulesLocation) {
        Remove-Item $modulesLocation -Recurse -Force -ErrorAction Ignore
    }

    $null = New-Item -Path $modulesLocation -Force -ItemType Directory

    Write-Verbose -Message "Starting dependency download" -Verbose
    $module = Get-Module -Name $modulePath -ListAvailable -ErrorAction Stop

    foreach ($requiredModule in $module.RequiredModules)
    {
        $saveParams = @{Name = $requiredModule.Name}
        if($requiredModule.Version)
        {
            $saveParams.Add('RequiredVersion',$requiredModule.Version)
        }

        # Download and save dependency nuget package
        Write-Verbose -Verbose -Message "Saving required module: $($requiredModule.Name)"
        Save-Package @saveParams -Path $modulesLocation -Source 'https://www.powershellgallery.com/api/v2' -ProviderName NuGet -AllowPreReleaseVersions:$AllowPreReleaseDependencies.IsPresent

        # Publish nuget package to local repository
        Write-Verbose -Verbose -Message "Publishing required module: $($requiredModule.Name)"
        $filterName = "$($requiredModule.Name)*.nupkg"
        $nupkgPath = (Get-ChildItem -Path $modulesLocation -Filter $filterName).FullName
        if (!$nupkgPath)
        {
            Write-Verbose -Verbose -Message "Dependent package name not found: $filterName"
        }
        else
        {
            Publish-Artifact -Path $nupkgPath -Name nupkg
        }
    }

    Write-Verbose -Message "Dependency download complete" -Verbose

    try {
        $repositoryExists = $null -ne (Get-PSResourceRepository -Name $sourceName -ErrorAction Ignore)
    }
    catch {
        $repositoryExists = $false
    }
    if ( !$repositoryExists) {
        Write-Verbose -Message "Register local repository" -Verbose
        Register-PSResourceRepository -Name $sourceName -Uri (Convert-ToUri $modulesLocation)
    }
 
    Write-Verbose -Verbose -Message "Starting to publish module: $modulePath"

    Publish-PSResource -Path $modulePath -Repository $sourceName -SkipDependenciesCheck

    Write-Verbose -Message "Local package published" -Verbose

    $nupkgPath = (Get-ChildItem -Path $modulesLocation -Filter "$($config.ModuleName)*.nupkg").FullName
    if (!$nupkgPath) {
        Write-Verbose -Verbose -Message "Publish location: $((Get-ChildItem -Path $modulesLocation -Recurse) | Out-String)"
        throw "$($config.ModuleName)*.nupkg not found in $modulesLocation"
    }
    
    Publish-Artifact -Path $nupkgPath -Name nupkg

    Write-Verbose -Message "Starting New-ProjectPackage" -Verbose
}

function Invoke-ProjectPublish {
    [CmdletBinding()]
    param(
        [Switch]
        $Signed,
        [Switch]
        $AllowPreReleaseDependencies
    )

    Write-Verbose -Verbose -Message "Publishing package ..."

    New-ProjectPackage -Signed:$Signed.IsPresent -AllowPreReleaseDependencies:$AllowPreReleaseDependencies.IsPresent -ErrorAction Stop

    Write-Verbose -Verbose -Message "Finished publishing package"
}

function RunPwshCommandInSubprocess {
    param(
        [string]
        $Command
    )

    if (-not $script:pwshPath) {
        $script:pwshPath = (Get-Process -Id $PID).Path
    }

    & $script:pwshPath -NoProfile -NoLogo -Command $Command
}

function Invoke-FunctionalValidation {
    param ( $tags = "CI" )
    $config = Get-ProjectConfiguration
    try {
        $testResultFile = "result.pester.xml"
        $testPath = $config.TestPath
        $modStage = "{0}/{1}" -f $config.BuildOutputPath,$config.ModuleName
        $command = @'
            Import-Module {0} -Force -Verbose
            Set-Location {1}
            Import-Module -Name Pester -MinimumVersion 5.0 -Verbose
            Invoke-Pester -Path . -OutputFile {2} -Tag "$tags"
'@ -f $modStage, $testPath, $testResultFile
        $output = RunPwshCommandInSubprocess -command $command | Foreach-Object { Write-Verbose -Verbose -Message $_ }
        return (Join-Path ${testPath} "$testResultFile")
    }
    catch {
        $output | Foreach-Object { Write-Warning "$_" }
        Write-Error "Error invoking tests"
    }
}

function GetPowerShellName {
    switch ($PSVersionTable.PSEdition) {
        'Core' {
            return 'PowerShell Core'
        }

        default {
            return 'Windows PowerShell'
        }
    }
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

function Test-PSPesterResult
{
    [CmdletBinding()]
    param(
        [Parameter()]
        [string] $TestResultsFile
    )

    if (!(Test-Path $TestResultsFile)) {
        throw "Test result file '$testResultsFile' not found for $TestArea."
    }

    $x = [xml](Get-Content -raw $testResultsFile)
    if ([int]$x.'test-results'.failures -gt 0) {
        Write-Error "TEST FAILURES"
        # switch between methods, SelectNode is not available on dotnet core
        if ( "System.Xml.XmlDocumentXPathExtensions" -as [Type] ) {
            $failures = [System.Xml.XmlDocumentXPathExtensions]::SelectNodes($x."test-results", './/test-case[@result = "Failure"]')
        }
        else {
            $failures = $x.SelectNodes('.//test-case[@result = "Failure"]')
        }
        foreach ( $testfail in $failures ) {
            Show-PSPesterError -testFailure $testfail
        }
    }
}

function Publish-AzDevOpsTestResult {
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSAvoidUsingWriteHost", "")]
    param(
        [parameter(Mandatory)]
        [string]
        $Path,
        [parameter(Mandatory)]
        [string]
        $Title,
        [string]
        $Type = 'NUnitXML',
        [bool]
        $FailTaskOnFailedTests = $true
    )

    $artifactPath = (Resolve-Path $Path).ProviderPath

    Write-Verbose -Verbose -Message "Uploading $artifactPath"

    # Just do nothing if we are not in AzDevOps
    if ($env:TF_BUILD) {
        $message = "vso[results.publish type=$Type;mergeResults=true;runTitle=$Title;publishRunAttachments=true;resultFiles=$artifactPath;failTaskOnFailedTests=$($FailTaskOnFailedTests.ToString().ToLowerInvariant());]"
        Write-Verbose -Message "sending AzDevOps: $message" -Verbose
        Write-Host "##$message"
    }
}

function Invoke-StaticValidation {

    $config = Get-ProjectConfiguration

    Write-Verbose -Message "Running ScriptAnalyzer" -Verbose
    $resultPSSA = RunScriptAnalysis -Location $config.BuildOutputPath

    Write-Verbose -Verbose -Message "PSSA result file: $resultPSSA"

    Write-Verbose -Message "Running BinSkim" -Verbose
    $resultBinSkim = Invoke-BinSkim -Location (Join-Path -Path $config.BuildOutputPath -ChildPath $config.ModuleName)

    Test-PSPesterResult -TestResultsFile $resultPSSA -ErrorAction Stop
    Test-PSPesterResult -TestResultsFile $resultBinSkim -ErrorAction Stop
}

function Invoke-ProjectTest {
    param(
        [Parameter()]
        [ValidateSet("Functional", "StaticAnalysis")]
        [string[]]
        $Type
    )

    END {
        if ($Type -contains "Functional" ) {
            # this will return a path to the results
            $resultFile = Invoke-FunctionalValidation
            Test-PSPesterResult -TestResultsFile $resultFile
            $powershellName = GetPowerShellName
            Publish-AzDevOpsTestResult -Path $resultFile -Title "Functional Tests -  $env:AGENT_OS - $powershellName Results" -Type NUnitXML
        }

        if ($Type -contains "StaticAnalysis" ) {
            Invoke-StaticValidation
        }
    }
}

using namespace System.Management.Automation
using namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
<#####################################################################################
 # File: Shared.psm1
 #
 # Copyright (c) Microsoft Corporation, 2020
 #####################################################################################>

try {
    $PSGetModulePath = Join-Path $psscriptroot "..\out\PowershellGet\PowerShellGet.psd1"
    Import-Module $PSGetModulePath -ErrorAction Stop
} catch [IO.FileNotFoundException] {
    throw [IO.FileNotFoundException]::new("Unable to find PowershellGet module at $PSGetModulePath, did you build it first with build.ps1?",$PSGetModulePath)
}

$DotnetCommandPath = @()
$EnvironmentVariableTarget = @{ Process = 0; User = 1; Machine = 2 }
$EnvPATHValueBackup = $null

$PowerShellGet = 'PowerShellGet'
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $IsWindows = $true
    $IsLinux = $false
    $IsMacOS = $false
    $IsCoreCLR = $false
}
$IsInbox = $PSHOME.EndsWith('\WindowsPowerShell\v1.0', [System.StringComparison]::OrdinalIgnoreCase)

$PSGalleryName = 'PSGallery'
$PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'

$PoshTestGalleryName = 'PoshTestGallery'
$PostTestGalleryLocation = 'https://www.poshtestgallery.com/api/v2'

if($IsInbox)
{
    $ProgramFilesPSPath = Microsoft.PowerShell.Management\Join-Path -Path $env:ProgramFiles -ChildPath "WindowsPowerShell"
}
elseif($IsCoreCLR){
    if($IsWindows) {
        $ProgramFilesPSPath = Microsoft.PowerShell.Management\Join-Path -Path $env:ProgramFiles -ChildPath 'PowerShell'
    }
    else {
        $ProgramFilesPSPath = Split-Path -Path ([System.Management.Automation.Platform]::SelectProductNameForDirectory('SHARED_MODULES')) -Parent
    }
}

try
{
    $MyDocumentsFolderPath = [Environment]::GetFolderPath("MyDocuments")
}
catch
{
    $MyDocumentsFolderPath = $null
}

if($IsInbox)
{
    $MyDocumentsPSPath = if($MyDocumentsFolderPath)
                                {
                                    Microsoft.PowerShell.Management\Join-Path -Path $MyDocumentsFolderPath -ChildPath "WindowsPowerShell"
                                }
                                else
                                {
                                    Microsoft.PowerShell.Management\Join-Path -Path $env:USERPROFILE -ChildPath "Documents\WindowsPowerShell"
                                }
}
elseif($IsCoreCLR) {
    if($IsWindows)
    {
        $MyDocumentsPSPath = if($MyDocumentsFolderPath)
        {
            Microsoft.PowerShell.Management\Join-Path -Path $MyDocumentsFolderPath -ChildPath 'PowerShell'
        }
        else
        {
            Microsoft.PowerShell.Management\Join-Path -Path $HOME -ChildPath "Documents\PowerShell"
        }
    }
    else
    {
        $MyDocumentsPSPath = Split-Path -Path ([System.Management.Automation.Platform]::SelectProductNameForDirectory('USER_MODULES')) -Parent
    }
}

$ProgramFilesModulesPath = Microsoft.PowerShell.Management\Join-Path -Path $ProgramFilesPSPath -ChildPath 'Modules'
$MyDocumentsModulesPath = Microsoft.PowerShell.Management\Join-Path -Path $MyDocumentsPSPath -ChildPath 'Modules'
$ProgramFilesScriptsPath = Microsoft.PowerShell.Management\Join-Path -Path $ProgramFilesPSPath -ChildPath 'Scripts'
$MyDocumentsScriptsPath = Microsoft.PowerShell.Management\Join-Path -Path $MyDocumentsPSPath -ChildPath 'Scripts'
$TempPath = [System.IO.Path]::GetTempPath()

if($IsWindows) {
    $PSGetProgramDataPath = Microsoft.PowerShell.Management\Join-Path -Path $env:ProgramData -ChildPath 'Microsoft\Windows\PowerShell\PowerShellGet\'
    $PSGetAppLocalPath = Microsoft.PowerShell.Management\Join-Path -Path $env:LOCALAPPDATA -ChildPath 'Microsoft\Windows\PowerShell\PowerShellGet\'
} else {
    $PSGetProgramDataPath = Join-Path -Path ([System.Management.Automation.Platform]::SelectProductNameForDirectory('CONFIG')) -ChildPath 'PowerShellGet'
    $PSGetAppLocalPath = Join-Path -Path ([System.Management.Automation.Platform]::SelectProductNameForDirectory('CACHE')) -ChildPath 'PowerShellGet'
}

$ProgramDataExePath = Microsoft.PowerShell.Management\Join-Path -Path $PSGetProgramDataPath -ChildPath $NuGetExeName
$ApplocalDataExePath = Microsoft.PowerShell.Management\Join-Path -Path $PSGetAppLocalPath -ChildPath $NuGetExeName
$moduleSourcesFilePath = Microsoft.PowerShell.Management\Join-Path -Path $PSGetAppLocalPath -ChildPath 'PSRepositories.xml'

# PowerShellGetFormatVersion will be incremented when we change the .nupkg format structure.
# PowerShellGetFormatVersion is in the form of Major.Minor.
# Minor is incremented for the backward compatible format change.
# Major is incremented for the breaking change.
$CurrentPSGetFormatVersion = "1.0"
$PSGetFormatVersionPrefix = "PowerShellGetFormatVersion_"

function Get-AllUsersModulesPath {
    return $ProgramFilesModulesPath
}

function Get-CurrentUserModulesPath {
    return $MyDocumentsModulesPath
}

function Get-AllUsersScriptsPath {
    return $ProgramFilesScriptsPath
}

function Get-CurrentUserScriptsPath {
    return $MyDocumentsScriptsPath
}

function Get-TempPath {
    return $TempPath
}

function Get-PSGetLocalAppDataPath {
    return $PSGetAppLocalPath
}

function Get-PSGalleryName
{
    return $PSGalleryName
}

function Get-PSGalleryLocation {
    return $PSGalleryLocation
}

function Get-PoshTestGalleryName {
    return $PoshTestGalleryName
}

function Get-PoshTestGalleryLocation {
    return $PostTestGalleryLocation
}

function Get-NewPSResourceRepositoryFile {
    # register our own repositories with desired priority
    $powerShellGetPath = Join-Path -Path ([Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)) -ChildPath "PowerShellGet"
    $originalXmlFilePath = Join-Path -Path $powerShellGetPath -ChildPath "PSResourceRepository.xml"
    $tempXmlFilePath = Join-Path -Path $powerShellGetPath -ChildPath "temp.xml"
    
    if (Test-Path -Path $originalXmlFilePath) {
        Copy-Item -Path $originalXmlFilePath -Destination $tempXmlFilePath
        Remove-Item -Path $originalXmlFilePath -Force -ErrorAction Ignore
    }

    if (! (Test-Path -Path $powerShellGetPath)) {
        $null = New-Item -Path $powerShellGetPath -ItemType Directory
    }

    $fileToCopy = Join-Path -Path $PSScriptRoot -ChildPath "testRepositories.xml"
    Copy-Item -Path $fileToCopy -Destination $originalXmlFilePath -Force
}

function Get-RevertPSResourceRepositoryFile {
    $powerShellGetPath = Join-Path -Path ([Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)) -ChildPath "PowerShellGet"
    $originalXmlFilePath = Join-Path -Path $powerShellGetPath -ChildPath "PSResourceRepository.xml"
    $tempXmlFilePath = Join-Path -Path $powerShellGetPath -ChildPath "temp.xml"

    if (Test-Path -Path $tempXmlFilePath) {
        Remove-Item -Path $originalXmlFilePath -Force -ErrorAction Ignore
        Copy-Item -Path $tempXmlFilePath -Destination $originalXmlFilePath -Force
        Remove-Item -Path $tempXmlFilePath -Force -ErrorAction Ignore
    }
}

function Get-TestDriveSetUp
{
    $repoURLAddress = Join-Path -Path $TestDrive -ChildPath "testdir"
    $null = New-Item $repoURLAddress -ItemType Directory -Force

    Set-PSResourceRepository -Name "psgettestlocal" -URL $repoURLAddress

    $SCRIPT:testResourcesFolder = Join-Path $TestDrive -ChildPath "TestLocalDirectory"
    
    $SCRIPT:testIndividualResourceFolder = Join-Path -Path $testResourcesFolder -ChildPath "PSGet_$(Get-Random)"
    $null = New-Item -Path $testIndividualResourceFolder -ItemType Directory -Force
}

function Get-RoleCapabilityResourcePublishedToLocalRepoTestDrive
{
    Param(
        [string]
        $roleCapName
    )

    Get-TestDriveSetUp

    $publishModuleName = $roleCapName
    $publishModuleBase = Join-Path $testIndividualResourceFolder $publishModuleName
    $null = New-Item -Path $publishModuleBase -ItemType Directory -Force

    $version = "1.0"
    New-PSRoleCapabilityFile -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psrc")
    New-ModuleManifest -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psd1") -ModuleVersion $version -Description "$publishModuleName module" -NestedModules "$publishModuleName.psm1" -DscResourcesToExport @('DefaultGatewayAddress', 'WINSSetting') -Tags @('PSDscResource_', 'DSC')

    Publish-PSResource -Path $publishModuleBase -Repository psgettestlocal
}

function Get-DSCResourcePublishedToLocalRepoTestDrive
{
    Param(
        [string]
        $dscName
    )

    Get-TestDriveSetUp

    $publishModuleName = $dscName
    $publishModuleBase = Join-Path $testIndividualResourceFolder $publishModuleName
    $null = New-Item -Path $publishModuleBase -ItemType Directory -Force

    $version = "1.0"
    New-ModuleManifest -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psd1") -ModuleVersion $version -Description "$publishModuleName module" -NestedModules "$publishModuleName.psm1" -DscResourcesToExport @('DefaultGatewayAddress', 'WINSSetting') -Tags @('PSDscResource_', 'DSC')

    Publish-PSResource -Path $publishModuleBase -Repository psgettestlocal
}

function Get-ScriptResourcePublishedToLocalRepoTestDrive
{
    Param(
        [string]
        $scriptName
    )
    Get-TestDriveSetUp

    $scriptFilePath = Join-Path -Path $testIndividualResourceFolder -ChildPath "$scriptName.ps1"
    $null = New-Item -Path $scriptFilePath -ItemType File -Force

    $version = "1.0.0"
    $params = @{
                #Path = $scriptFilePath
                Version = $version
                #GUID = 
                Author = 'Jane'
                CompanyName = 'Microsoft Corporation'
                Copyright = '(c) 2020 Microsoft Corporation. All rights reserved.'
                Description = "Description for the $scriptName script"
                LicenseUri = "https://$scriptName.com/license"
                IconUri = "https://$scriptName.com/icon"
                ProjectUri = "https://$scriptName.com"
                Tags = @('Tag1','Tag2', "Tag-$scriptName-$version")
                ReleaseNotes = "$scriptName release notes"
                }

    $scriptMetadata = New-PSScriptMetadata @params
    Set-Content -Path $scriptFilePath -Value $scriptMetadata

    Publish-PSResource -path $scriptFilePath -Repository psgettestlocal
}

function Get-CommandResourcePublishedToLocalRepoTestDrive
{
    Param(
        [string]
        $cmdName
    )
    Get-TestDriveSetUp

    $publishModuleName = $cmdName
    $publishModuleBase = Join-Path $testIndividualResourceFolder $publishModuleName
    $null = New-Item -Path $publishModuleBase -ItemType Directory -Force

    $version = "1.0"
    New-ModuleManifest -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psd1") -ModuleVersion $version -Description "$publishModuleName module" -NestedModules "$publishModuleName.psm1" -CmdletsToExport @('Get-Test', 'Set-Test')

    Publish-PSResource -Path $publishModuleBase -Repository psgettestlocal
}

function Get-ModuleResourcePublishedToLocalRepoTestDrive
{
    Param(
        [string]
        $moduleName
    )
    Get-TestDriveSetUp

    $publishModuleName = $moduleName
    $publishModuleBase = Join-Path $testIndividualResourceFolder $publishModuleName
    $null = New-Item -Path $publishModuleBase -ItemType Directory -Force

    $version = "1.0"
    New-ModuleManifest -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psd1") -ModuleVersion $version -Description "$publishModuleName module" -NestedModules "$publishModuleName.psm1"

    Publish-PSResource -Path $publishModuleBase -Repository psgettestlocal
}

function RemoveItem
{
    Param(
        [string]
        $path
    )

    if($path -and (Test-Path $path))
    {
        Remove-Item $path -Force -Recurse -ErrorAction SilentlyContinue
    }
}

function New-PSScriptMetadata
{
    [OutputType([String])]
    [CmdletBinding(PositionalBinding=$false)]

    Param
    (
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]
        $Version,

        [Parameter()]
        #[ValidateNotNullOrEmpty()]
        [Guid]
        $Guid,

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]
        $Author,

        [Parameter()]
        [String]
        $CompanyName,

        [Parameter()]
        [string]
        $Copyright,

        [Parameter()]
        [string]
        $Description,

        [Parameter()]
        [String[]]
        $ExternalModuleDependencies,

        [Parameter()]
        [string[]]
        $RequiredScripts,

        [Parameter()]
        [String[]]
        $ExternalScriptDependencies,

        [Parameter()]
        [string[]]
        $Tags,

        [Parameter()]
        [Uri]
        $ProjectUri,

        [Parameter()]
        [Uri]
        $LicenseUri,

        [Parameter()]
        [Uri]
        $IconUri,

        [Parameter()]
        [string[]]
        $ReleaseNotes,

		[Parameter()]
        [string]
        $PrivateData
    )

    Process
    {
        $PSScriptInfoString = @"

<#PSScriptInfo

.VERSION$(if ($Version) {" $Version"})

.GUID$(if ($Guid) {" $Guid"})

.AUTHOR$(if ($Author) {" $Author"})

.COMPANYNAME$(if ($CompanyName) {" $CompanyName"})

.COPYRIGHT$(if ($Copyright) {" $Copyright"})

.DESCRIPTION$(if ($Description) {" $Description"})

.TAGS$(if ($Tags) {" $Tags"})

.LICENSEURI$(if ($LicenseUri) {" $LicenseUri"})

.PROJECTURI$(if ($ProjectUri) {" $ProjectUri"})

.ICONURI$(if ($IconUri) {" $IconUri"})

.EXTERNALMODULEDEPENDENCIES$(if ($ExternalModuleDependencies) {" $($ExternalModuleDependencies -join ',')"}) 

.REQUIREDSCRIPTS$(if ($RequiredScripts) {" $($RequiredScripts -join ',')"})

.EXTERNALSCRIPTDEPENDENCIES$(if ($ExternalScriptDependencies) {" $($ExternalScriptDependencies -join ',')"})

.RELEASENOTES
$($ReleaseNotes -join "`r`n")

.PRIVATEDATA$(if ($PrivateData) {" $PrivateData"})

#>
"@
        return $PSScriptInfoString
    }
}
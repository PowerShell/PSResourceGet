<#####################################################################################
 # File: PSGetTestUtils.psm1
 #
 # Copyright (c) Microsoft Corporation, 2020
 #####################################################################################>

#."$PSScriptRoot\uiproxy.ps1"

$psGetMod = Get-Module -Name PowerShellGet
if ((! $psGetMod) -or (($psGetMod | Select-Object Version) -lt 3.0.0))
{
    Write-Verbose -Verbose "Importing PowerShellGet 3.0.0 for test"
    Import-Module -Name PowerShellGet -MinimumVersion 3.0.0 -Force
}

$script:DotnetCommandPath = @()
$script:EnvironmentVariableTarget = @{ Process = 0; User = 1; Machine = 2 }
$script:EnvPATHValueBackup = $null

$script:PowerShellGet = 'PowerShellGet'
$script:IsInbox = $PSHOME.EndsWith('\WindowsPowerShell\v1.0', [System.StringComparison]::OrdinalIgnoreCase)
$script:IsWindows = (-not (Get-Variable -Name IsWindows -ErrorAction Ignore)) -or $IsWindows
$script:IsLinux = (Get-Variable -Name IsLinux -ErrorAction Ignore) -and $IsLinux
$script:IsMacOS = (Get-Variable -Name IsMacOS -ErrorAction Ignore) -and $IsMacOS
$script:IsCoreCLR = $PSVersionTable.ContainsKey('PSEdition') -and $PSVersionTable.PSEdition -eq 'Core'

$script:PSGalleryName = 'PSGallery'
$script:PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'

$script:PoshTestGalleryName = 'PoshTestGallery'
$script:PostTestGalleryLocation = 'https://www.poshtestgallery.com/api/v2'

if($script:IsInbox)
{
    $script:ProgramFilesPSPath = Microsoft.PowerShell.Management\Join-Path -Path $env:ProgramFiles -ChildPath "WindowsPowerShell"
}
elseif($script:IsCoreCLR){
    if($script:IsWindows) {
        $script:ProgramFilesPSPath = Microsoft.PowerShell.Management\Join-Path -Path $env:ProgramFiles -ChildPath 'PowerShell'
    }
    else {
        $script:ProgramFilesPSPath = Split-Path -Path ([System.Management.Automation.Platform]::SelectProductNameForDirectory('SHARED_MODULES')) -Parent
    }
}

try
{
    $script:MyDocumentsFolderPath = [Environment]::GetFolderPath("MyDocuments")
}
catch
{
    $script:MyDocumentsFolderPath = $null
}

if($script:IsInbox)
{
    $script:MyDocumentsPSPath = if($script:MyDocumentsFolderPath)
                                {
                                    Microsoft.PowerShell.Management\Join-Path -Path $script:MyDocumentsFolderPath -ChildPath "WindowsPowerShell"
                                }
                                else
                                {
                                    Microsoft.PowerShell.Management\Join-Path -Path $env:USERPROFILE -ChildPath "Documents\WindowsPowerShell"
                                }
}
elseif($script:IsCoreCLR) {
    if($script:IsWindows)
    {
        $script:MyDocumentsPSPath = if($script:MyDocumentsFolderPath)
        {
            Microsoft.PowerShell.Management\Join-Path -Path $script:MyDocumentsFolderPath -ChildPath 'PowerShell'
        }
        else
        {
            Microsoft.PowerShell.Management\Join-Path -Path $HOME -ChildPath "Documents\PowerShell"
        }
    }
    else
    {
        $script:MyDocumentsPSPath = Split-Path -Path ([System.Management.Automation.Platform]::SelectProductNameForDirectory('USER_MODULES')) -Parent
    }
}

$script:ProgramFilesModulesPath = Microsoft.PowerShell.Management\Join-Path -Path $script:ProgramFilesPSPath -ChildPath 'Modules'
$script:MyDocumentsModulesPath = Microsoft.PowerShell.Management\Join-Path -Path $script:MyDocumentsPSPath -ChildPath 'Modules'
$script:ProgramFilesScriptsPath = Microsoft.PowerShell.Management\Join-Path -Path $script:ProgramFilesPSPath -ChildPath 'Scripts'
$script:MyDocumentsScriptsPath = Microsoft.PowerShell.Management\Join-Path -Path $script:MyDocumentsPSPath -ChildPath 'Scripts'
$script:TempPath = [System.IO.Path]::GetTempPath()

if($script:IsWindows) {
    $script:PSGetProgramDataPath = Microsoft.PowerShell.Management\Join-Path -Path $env:ProgramData -ChildPath 'Microsoft\Windows\PowerShell\PowerShellGet\'
    $script:PSGetAppLocalPath = Microsoft.PowerShell.Management\Join-Path -Path $env:LOCALAPPDATA -ChildPath 'Microsoft\Windows\PowerShell\PowerShellGet\'
} else {
    $script:PSGetProgramDataPath = Join-Path -Path ([System.Management.Automation.Platform]::SelectProductNameForDirectory('CONFIG')) -ChildPath 'PowerShellGet'
    $script:PSGetAppLocalPath = Join-Path -Path ([System.Management.Automation.Platform]::SelectProductNameForDirectory('CACHE')) -ChildPath 'PowerShellGet'
}

$script:ProgramDataExePath = Microsoft.PowerShell.Management\Join-Path -Path $script:PSGetProgramDataPath -ChildPath $script:NuGetExeName
$script:ApplocalDataExePath = Microsoft.PowerShell.Management\Join-Path -Path $script:PSGetAppLocalPath -ChildPath $script:NuGetExeName
$script:moduleSourcesFilePath = Microsoft.PowerShell.Management\Join-Path -Path $script:PSGetAppLocalPath -ChildPath 'PSRepositories.xml'

# PowerShellGetFormatVersion will be incremented when we change the .nupkg format structure.
# PowerShellGetFormatVersion is in the form of Major.Minor.
# Minor is incremented for the backward compatible format change.
# Major is incremented for the breaking change.
$script:CurrentPSGetFormatVersion = "1.0"
$script:PSGetFormatVersionPrefix = "PowerShellGetFormatVersion_"

function Get-AllUsersModulesPath {
    return $script:ProgramFilesModulesPath
}

function Get-CurrentUserModulesPath {
    return $script:MyDocumentsModulesPath
}

function Get-AllUsersScriptsPath {
    return $script:ProgramFilesScriptsPath
}

function Get-CurrentUserScriptsPath {
    return $script:MyDocumentsScriptsPath
}

function Get-TempPath {
    return $script:TempPath
}

function Get-PSGetLocalAppDataPath {
    return $script:PSGetAppLocalPath
}

function Get-PSGalleryName
{
    return $script:PSGalleryName
}

function Get-PSGalleryLocation {
    return $script:PSGalleryLocation
}

function Get-PoshTestGalleryName {
    return $script:PoshTestGalleryName
}

function Get-PoshTestGalleryLocation {
    return $script:PostTestGalleryLocation
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
        $null = New-Item -Path $powerShellGetPath -ItemType Directory -Verbose
    }

    $fileToCopy = Join-Path -Path $PSScriptRoot -ChildPath "testRepositories.xml"
    Copy-Item -Path $fileToCopy -Destination $originalXmlFilePath -Force -Verbose
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

    $testResourcesFolder = Join-Path $TestDrive -ChildPath "TestLocalDirectory"

    $script:testIndividualResourceFolder = Join-Path -Path $testResourcesFolder -ChildPath "PSGet_$(Get-Random)"
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
    $publishModuleBase = Join-Path $script:testIndividualResourceFolder $publishModuleName
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
    $publishModuleBase = Join-Path $script:testIndividualResourceFolder $publishModuleName
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

    $scriptFilePath = Join-Path -Path $script:testIndividualResourceFolder -ChildPath "$scriptName.ps1"
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

    $scriptMetadata = Create-PSScriptMetadata @params
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
    $publishModuleBase = Join-Path $script:testIndividualResourceFolder $publishModuleName
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
    $publishModuleBase = Join-Path $script:testIndividualResourceFolder $publishModuleName
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

function Create-PSScriptMetadata
{
    [OutputType([String])]
    [CmdletBinding(PositionalBinding=$false,
    SupportsShouldProcess=$true)]

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

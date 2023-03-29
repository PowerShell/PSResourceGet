# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$script:DotnetCommandPath = @()
$script:EnvironmentVariableTarget = @{ Process = 0; User = 1; Machine = 2 }
$script:EnvPATHValueBackup = $null

$script:PowerShellGet = 'PowerShellGet'
$script:IsInbox = $PSHOME.EndsWith('\WindowsPowerShell\v1.0', [System.StringComparison]::OrdinalIgnoreCase)
$script:IsWindows = $IsWindows
if ($IsWindows -eq $null) {
    $script:IsWindows = ($PSVersionTable.PSVersion.Major -eq 5)
}

$script:IsLinux = (Get-Variable -Name IsLinux -ErrorAction Ignore) -and $IsLinux
$script:IsMacOS = (Get-Variable -Name IsMacOS -ErrorAction Ignore) -and $IsMacOS
$script:IsCoreCLR = $PSVersionTable.ContainsKey('PSEdition') -and $PSVersionTable.PSEdition -eq 'Core'

$script:PSGalleryName = 'PSGallery'
$script:PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'

$script:NuGetGalleryName = 'NuGetGallery'
$script:NuGetGalleryLocation = 'https://api.nuget.org/v3/index.json'

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

function Test-IsAdmin {
    [OutputType([bool])]
    param()

    [System.Security.Principal.WindowsPrincipal]::new(
        [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-IsWindows {
    return $script:IsWindows
}

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

function Get-NuGetGalleryName
{
    return $script:NuGetGalleryName
}

function Get-NuGetGalleryLocation
{
    return $script:NuGetGalleryLocation
}

function Get-PSGalleryName
{
    return $script:PSGalleryName
}

function Get-PSGalleryLocation {
    return $script:PSGalleryLocation
}
function Get-NewTestDirs {
    Param(
        [string[]]
        $listOfPaths
    )
    foreach($path in $listOfPaths)
    {
        $null = New-Item -Path $path -ItemType Directory
    }
}

function Get-RemoveTestDirs {
    Param(
        [string[]]
        $listOfPaths
    )
    foreach($path in $listOfPaths)
    {
        if(Test-Path -Path $path)
        {
            Remove-Item -Path $path -Force -ErrorAction Ignore
        }
    }
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

function Get-NewPSResourceRepositoryFileWithCredentialInfo {
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

    $fileToCopy = Join-Path -Path $PSScriptRoot -ChildPath "testRepositoriesWithCredentialInfo.xml"
    Copy-Item -Path $fileToCopy -Destination $originalXmlFilePath -Force -Verbose
}

function Register-LocalRepos {
    $repoUriAddress = Join-Path -Path $TestDrive -ChildPath "testdir"
    $null = New-Item $repoUriAddress -ItemType Directory -Force
    $localRepoParams = @{
        Name = "psgettestlocal"
        Uri = $repoUriAddress
        Priority = 40
        Trusted = $false
    }
    Register-PSResourceRepository @localRepoParams

    $repoUriAddress2 = Join-Path -Path $TestDrive -ChildPath "testdir2"
    $null = New-Item $repoUriAddress2 -ItemType Directory -Force
    $localRepoParams2 = @{
        Name = "psgettestlocal2"
        Uri = $repoUriAddress2
        Priority = 50
        Trusted = $false
    }
    Register-PSResourceRepository @localRepoParams2
    Write-Verbose("registered psgettestlocal, psgettestlocal2")
}

function Register-PSGallery {
    $PSGalleryRepoParams = @{
        Name = $script:PSGalleryName
        Uri = $script:PSGalleryLocation
        Priority = 1
        Trusted = $false
    }
    Register-PSResourceRepository @PSGalleryRepoParams

    Write-Verbose("registered PSGallery")
}

function Unregister-LocalRepos {
    if(Get-PSResourceRepository -Name "psgettestlocal"){
        Unregister-PSResourceRepository -Name "psgettestlocal"
    }
    if(Get-PSResourceRepository -Name "psgettestlocal2"){
        Unregister-PSResourceRepository -Name "psgettestlocal2"
    }
}
function Get-TestDriveSetUp
{
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
        $scriptName,

        [string]
        $scriptRepoName,

        [string]
        $scriptVersion
    )
    Get-TestDriveSetUp

    $scriptFilePath = Join-Path -Path $script:testIndividualResourceFolder -ChildPath "$scriptName.ps1"
    $null = New-Item -Path $scriptFilePath -ItemType File -Force

    $params = @{
                Version = $scriptVersion
                GUID = [guid]::NewGuid()
                Author = 'Jane'
                CompanyName = 'Microsoft Corporation'
                Copyright = '(c) 2020 Microsoft Corporation. All rights reserved.'
                Description = "Description for the $scriptName script"
                LicenseUri = "https://$scriptName.com/license"
                IconUri = "https://$scriptName.com/icon"
                ProjectUri = "https://$scriptName.com"
                Tags = @('Tag1','Tag2', "Tag-$scriptName-$scriptVersion")
                ReleaseNotes = "$scriptName release notes"
                }

    $scriptMetadata = Create-PSScriptMetadata @params
    Set-Content -Path $scriptFilePath -Value $scriptMetadata
    Publish-PSResource -Path $scriptFilePath -Repository $scriptRepoName -Verbose
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
        $moduleName,

        [string]
        $repoName
    )
    Get-TestDriveSetUp

    $publishModuleName = $moduleName
    $publishModuleBase = Join-Path $script:testIndividualResourceFolder $publishModuleName
    $null = New-Item -Path $publishModuleBase -ItemType Directory -Force

    $version = "1.0"
    New-ModuleManifest -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psd1") -ModuleVersion $version -Description "$publishModuleName module"

    Publish-PSResource -Path $publishModuleBase -Repository $repoName
}

function Create-TagsStringEntry
{
    Param(
        [string[]]
        $tags
    )

    if (!$tags)
    {
        return ""
    }

    $tagsString = "Tags = @(" + ($tags -join ",") + ")"
    return $tagsString
}

function New-TestModule
{
    Param(
        [string]
        $path = "$TestDrive",

        [string]
        $moduleName = "TestModule",

        [string]
        $repoName,

        [string]
        $packageVersion,

        [string]
        $prereleaseLabel,

        [string[]]
        $tags
    )

    $modulePath = Join-Path -Path $path -ChildPath $ModuleName
    $moduleMan = Join-Path $modulePath -ChildPath ($ModuleName + '.psd1')

    if ( Test-Path -Path $modulePath) {
        Remove-Item -Path $modulePath -Recurse -Force
    }

    $null = New-Item -Path $modulePath -ItemType Directory -Force
    $tagsEntry = Create-TagsStringEntry -tags $tags
    $prereleaseEntry = ""
    if ($prereleaseLabel)
    {
        $prereleaseEntry = "Prerelease = '{0}'" -f $prereleaseLabel
    }

    @'
    @{{
        ModuleVersion     = '{0}'
        Author            = 'None'
        Description       = 'None'
        GUID              = '0c2829fc-b165-4d72-9038-ae3a71a755c1'
        FunctionsToExport = @()
        RequiredModules   = @()
        PrivateData = @{{
            PSData = @{{
                {1}
                {2}
            }}
        }}
    }}
'@ -f $packageVersion, $prereleaseEntry, $tagsEntry | Out-File -FilePath $moduleMan

    Publish-PSResource -Path $modulePath -Repository $repoName
}

function Get-ModuleResourcePublishedToLocalRepoTestDrive
{
    Param(
        [string]
        $moduleName,

        [string]
        $repoName,

        [string]
        $packageVersion,

        [string]
        $prereleaseLabel,

        [string[]]
        $tags
    )
    Get-TestDriveSetUp

    $publishModuleName = $moduleName
    $publishModuleBase = Join-Path $script:testIndividualResourceFolder $publishModuleName
    $null = New-Item -Path $publishModuleBase -ItemType Directory -Force

    $version = $packageVersion
    if (!$tags -or ($tags.Count -eq 0))
    {
        if (!$prereleaseLabel)
        {
            New-ModuleManifest -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psd1") -ModuleVersion $version -Description "$publishModuleName module"
        }
        else
        {
            New-ModuleManifest -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psd1") -ModuleVersion $version -Prerelease $prereleaseLabel -Description "$publishModuleName module"
        }
    }
    else {
        # tags is not null or is empty
        if (!$prereleaseLabel)
        {
            New-ModuleManifest -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psd1") -ModuleVersion $version -Description "$publishModuleName module" -Tags $tags
        }
        else
        {
            New-ModuleManifest -Path (Join-Path -Path $publishModuleBase -ChildPath "$publishModuleName.psd1") -ModuleVersion $version -Prerelease $prereleaseLabel -Description "$publishModuleName module" -Tags $tags
        }
    }

    Publish-PSResource -Path $publishModuleBase -Repository $repoName
}

function Register-LocalRepos {
    $repoUriAddress = Join-Path -Path $TestDrive -ChildPath "testdir"
    $null = New-Item $repoUriAddress -ItemType Directory -Force
    $localRepoParams = @{
        Name = "psgettestlocal"
        Uri = $repoUriAddress
        Priority = 40
        Trusted = $false
    }

    Register-PSResourceRepository @localRepoParams

    $repoUriAddress2 = Join-Path -Path $TestDrive -ChildPath "testdir2"
    $null = New-Item $repoUriAddress2 -ItemType Directory -Force
    $localRepoParams2 = @{
        Name = "psgettestlocal2"
        Uri = $repoUriAddress2
        Priority = 50
        Trusted = $false
    }

    Register-PSResourceRepository @localRepoParams2
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

<#

.DESCRIPTION
$(if ($Description) {" $Description"})

#>
"@
        return $PSScriptInfoString
    }
}

<#
Checks that provided PSGetInfo object contents match the expected data
from the test information file: PSGetModuleInfo.xml
#>

function CheckForExpectedPSGetInfo
{
    param ($psGetInfo)

    $psGetInfo.AdditionalMetadata.Keys | Should -HaveCount 22
    $psGetInfo.AdditionalMetadata['copyright'] | Should -BeExactly '(c) Microsoft Corporation. All rights reserved.'
    $psGetInfo.AdditionalMetadata['description'] | Should -BeLike 'This module provides a convenient way for a user to store and retrieve secrets*'
    $psGetInfo.AdditionalMetadata['requireLicenseAcceptance'] | Should -BeExactly 'False'
    $psGetInfo.AdditionalMetadata['isLatestVersion'] | Should -BeExactly 'True'
    $psGetInfo.AdditionalMetadata['isAbsoluteLatestVersion'] | Should -BeExactly 'True'
    $psGetInfo.AdditionalMetadata['versionDownloadCount'] | Should -BeExactly '0'
    $psGetInfo.AdditionalMetadata['downloadCount'] | Should -BeExactly '15034'
    $psGetInfo.AdditionalMetadata['packageSize'] | Should -BeExactly '55046'
    $psGetInfo.AdditionalMetadata['published'] | Should -BeExactly '3/25/2021 6:08:10 PM -07:00'
    $psGetInfo.AdditionalMetadata['created'] | Should -BeExactly '3/25/2021 6:08:10 PM -07:00'
    $psGetInfo.AdditionalMetadata['lastUpdated'] | Should -BeExactly '3/25/2021 6:08:10 PM -07:00'
    $psGetInfo.AdditionalMetadata['tags'] | Should -BeLike 'PSModule PSEdition_Core PSCmdlet_Register-SecretVault*'
    $psGetInfo.AdditionalMetadata['developmentDependency'] | Should -BeExactly 'False'
    $psGetInfo.AdditionalMetadata['updated'] | Should -BeExactly '2021-03-25T18:08:10Z'
    $psGetInfo.AdditionalMetadata['NormalizedVersion'] | Should -BeExactly '1.1.0-preview2'
    $psGetInfo.AdditionalMetadata['Authors'] | Should -BeExactly 'Microsoft Corporation'
    $psGetInfo.AdditionalMetadata['IsPrerelease'] | Should -Be $True
    $psGetInfo.AdditionalMetadata['ItemType'] | Should -BeExactly 'Module'
    $psGetInfo.AdditionalMetadata['FileList'] | Should -BeLike 'Microsoft.PowerShell.SecretManagement.nuspec|Microsoft.PowerShell.SecretManagement.dll*'
    $psGetInfo.AdditionalMetadata['GUID'] | Should -BeExactly 'a5c858f6-4a8e-41f1-b1ee-0ff8f6ad69d3'
    $psGetInfo.AdditionalMetadata['PowerShellVersion'] | Should -BeExactly '5.1'
    $psGetInfo.AdditionalMetadata['CompanyName'] | Should -BeExactly 'Microsoft Corporation'
    #
    $psGetInfo.Author | Should -BeExactly 'Microsoft Corporation'
    $psGetInfo.CompanyName | Should -BeExactly 'Microsoft Corporation'
    $psGetInfo.Copyright | Should -BeExactly '(c) Microsoft Corporation. All rights reserved.'
    $psGetInfo.Dependencies | Should -HaveCount 0
    $psGetInfo.Description | Should -BeLike 'This module provides a convenient way for a user to store*'
    $psGetInfo.IconUri | Should -BeNullOrEmpty
    $psGetInfo.IsPrerelease | Should -Be $True
    $psGetInfo.Prerelease | Should -Be "preview2"
    $psGetInfo.Includes.Cmdlet | Should -HaveCount 10
    $psGetInfo.Includes.Cmdlet[0] | Should -BeExactly 'Register-SecretVault'
    $psGetInfo.InstalledDate.Year | Should -BeExactly 2021
    $psGetInfo.InstalledLocation | Should -BeLike 'C:\Users\*'
    $psGetInfo.LicenseUri | Should -BeExactly 'https://github.com/PowerShell/SecretManagement/blob/master/LICENSE'
    $psGetInfo.Name | Should -BeExactly 'Microsoft.PowerShell.SecretManagement'
    $psGetInfo.PackageManagementProvider | Should -BeExactly 'NuGet'
    $psGetInfo.PowerShellGetFormatVersion | Should -BeNullOrEmpty
    $psGetInfo.ProjectUri | Should -BeExactly 'https://github.com/powershell/secretmanagement'
    $psGetInfo.PublishedDate.Year | Should -BeExactly 2021
    $psGetInfo.ReleasedNotes | Should -BeNullOrEmpty
    $psGetInfo.Repository | Should -BeExactly 'PSGallery'
    $psGetInfo.RepositorySourceLocation | Should -BeExactly 'https://www.powershellgallery.com/api/v2'
    $psGetInfo.Tags | Should -BeExactly @('PSModule', 'PSEdition_Core')
    $psGetInfo.Type | Should -BeExactly 'Module'
    $psGetInfo.UpdatedDate.Year | Should -BeExactly 1
    $psGetInfo.Version | Should -Be "1.1.0"
}

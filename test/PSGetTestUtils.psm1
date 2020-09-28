<#####################################################################################
 # File: PSGetTestUtils.psm1
 #
 # Copyright (c) Microsoft Corporation, 2020
 #####################################################################################>

#."$PSScriptRoot\uiproxy.ps1"

$baseParentPath = Split-Path -Path $PSScriptRoot # removes test directory and returns remaning parent path
$fullPath = Join-Path -Path $baseParentPath -ChildPath "src" -AdditionalChildPath "out", "PowerShellGet"
Import-Module $fullPath -Force

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
    $originalXmlFilePath = Join-Path -Path $env:LOCALAPPDATA -ChildPath "PowerShellGet" -AdditionalChildPath "PSResourceRepository.xml"
    $tempXmlFilePath = Join-Path -Path $env:LOCALAPPDATA -ChildPath "PowerShellGet" -AdditionalChildPath "temp.xml"
    Copy-Item -Path $originalXmlFilePath -Destination  $tempXmlFilePath

    Remove-Item -Path $originalXmlFilePath -Force

    $fileToCopy = Join-Path -Path $PSScriptRoot -ChildPath "testRepositories.xml"
    Copy-Item $fileToCopy -Destination $originalXmlFilePath

}

function Get-RevertPSResourceRepositoryFile {
    $originalXmlFilePath = Join-Path -Path $env:LOCALAPPDATA -ChildPath "PowerShellGet" -AdditionalChildPath "PSResourceRepository.xml"
    $tempXmlFilePath = Join-Path -Path $env:LOCALAPPDATA -ChildPath "PowerShellGet" -AdditionalChildPath "temp.xml"

    Remove-Item -Path $originalXmlFilePath -Force
    
    Copy-Item -Path $tempXmlFilePath -Destination $originalXmlFilePath

    Remove-Item $tempXmlFilePath
}

function Get-LocalRepoSetUp
{
    $repoURLAddress = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "testdir"
    $null = New-Item -Path $repoURLAddress -ItemType Directory -Force 

    Set-PSResourceRepository -Name "psgettestlocal" -URL $repoURLAddress

    $TestLocalDirectory = 'TestLocalDirectory'
    $tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $TestLocalDirectory

    $script:TempModulesPath = Join-Path -Path $tmpdir -ChildPath "PSGet_$(Get-Random)"
    $null = New-Item -Path $script:TempModulesPath -ItemType Directory -Force
}

function Get-RoleCapabilityResourcePublishedToLocalRepo
{
    Param(
        [string]
        $roleCapName
    )

    Get-LocalRepoSetUp

    $PublishModuleName = $roleCapName
    $PublishModuleBase = Join-Path $TempModulesPath $PublishModuleName
    $null = New-Item -Path $PublishModuleBase -ItemType Directory -Force

    $PublishModuleBase = Join-Path $TempModulesPath $PublishModuleName
    $version = "1.0"

    New-PSRoleCapabilityFile -Path (Join-Path -Path $PublishModuleBase -ChildPath "$PublishModuleName.psrc")
    New-ModuleManifest -Path (Join-Path -Path $PublishModuleBase -ChildPath "$PublishModuleName.psd1") -ModuleVersion $version -Description "$PublishModuleName module"  -NestedModules "$PublishModuleName.psm1"

    Publish-PSResource -path  $PublishModuleBase -Repository psgettestlocal
}

function Get-DSCResourcePublishedToLocalRepo {
    Param(
        [string]
        $dscName
    )

    Get-LocalRepoSetUp

    $PublishModuleName = $dscName
    $PublishModuleBase = Join-Path $script:TempModulesPath $PublishModuleName
    $null = New-Item -Path $PublishModuleBase -ItemType Directory -Force

    $PublishModuleBase = Join-Path $script:TempModulesPath $PublishModuleName
    $version = "1.0"
    New-ModuleManifest -Path (Join-Path -Path $PublishModuleBase -ChildPath "$PublishModuleName.psd1") -ModuleVersion $version -Description "$PublishModuleName module"  -NestedModules "$PublishModuleName.psm1" -DscResourcesToExport @('DefaultGatewayAddress', 'WINSSetting') -Tags @('PSDscResource_', 'DSC')

    Publish-PSResource -path  $PublishModuleBase -Repository psgettestlocal
}

function Get-ScriptResourcePublishedToLocalRepo {
    Param(
        [string]
        $scriptName
    )
    Get-LocalRepoSetUp

    $Name = $scriptName
    $scriptFilePath = Join-Path -Path $script:TempModulesPath -ChildPath "$Name.ps1"
    $null = New-Item -Path $scriptFilePath -ItemType File -Force

    $version = "1.0.0"
    $params = @{
                #Path = $scriptFilePath
                Version = $version
                #GUID = 
                Author = 'Jane'
                CompanyName = 'Microsoft Corporation'
                Copyright = '(c) 2020 Microsoft Corporation. All rights reserved.'
                Description = "Description for the $Name script"
                LicenseUri = "https://$Name.com/license"
                IconUri = "https://$Name.com/icon"
                ProjectUri = "https://$Name.com"
                Tags = @('Tag1','Tag2', "Tag-$Name-$version")
                ReleaseNotes = "$Name release notes"
                }

    $scriptMetadata = Create-PSScriptMetadata @params
    Set-Content -Path $scriptFilePath -Value $scriptMetadata

    Publish-PSResource -path $scriptFilePath -Repository psgettestlocal
}

function Get-CommandResourcePublishedToLocalRepo {
    Param(
        [string]
        $cmdName
    )
    Get-LocalRepoSetUp

    $PublishModuleName = $cmdName
    $PublishModuleBase = Join-Path $script:TempModulesPath $PublishModuleName
    $null = New-Item -Path $PublishModuleBase -ItemType Directory -Force

    $PublishModuleBase = Join-Path $script:TempModulesPath $PublishModuleName
    $version = "1.0"
    New-ModuleManifest -Path (Join-Path -Path $PublishModuleBase -ChildPath "$PublishModuleName.psd1") -ModuleVersion $version -Description "$PublishModuleName module"  -NestedModules "$PublishModuleName.psm1" -CmdletsToExport @('Get-Test', 'Set-Test')

    Publish-PSResource -path  $PublishModuleBase -Repository psgettestlocal
}
function Get-ModuleResourcePublishedToLocalRepo {
    Param(
        [string]
        $modName
    )
    Get-LocalRepoSetUp

    $PublishModuleName = $modName
    $PublishModuleBase = Join-Path $script:TempModulesPath $PublishModuleName
    $null = New-Item -Path $PublishModuleBase -ItemType Directory -Force

    $PublishModuleBase = Join-Path $script:TempModulesPath $PublishModuleName
    $version = "1.0"
    New-ModuleManifest -Path (Join-Path -Path $PublishModuleBase -ChildPath "$PublishModuleName.psd1") -ModuleVersion $version -Description "$PublishModuleName module"  -NestedModules "$PublishModuleName.psm1"
    Publish-PSResource -path  $PublishModuleBase -Repository psgettestlocal
}


function RemoveTmpdir
{
    $TestLocalDirectory = 'TestLocalDirectory'
    $tmpdir = Join-Path -Path $script:TempPath -ChildPath $TestLocalDirectory
    if($tmpdir -and (Test-Path $tmpdir))
    {
        Remove-Item $tmpdir -Force -Recurse -ErrorAction SilentlyContinue
    }
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

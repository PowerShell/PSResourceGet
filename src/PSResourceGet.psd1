# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

@{
    RootModule        = './PSResourceGet.dll'
    ModuleVersion     = '0.5.22'
    CompatiblePSEditions = @('Core', 'Desktop')
    GUID              = 'e4e0bda1-0703-44a5-b70d-8fe704cd0643'
    Author            = 'Microsoft Corporation'
    CompanyName       = 'Microsoft Corporation'
    Copyright         = '(c) Microsoft Corporation. All rights reserved.'
    Description       = 'PowerShell module with commands for discovering, installing, updating and publishing the PowerShell artifacts like Modules, Scripts, and DSC Resources.'
    PowerShellVersion = '5.1'
    DotNetFrameworkVersion = '2.0'
    CLRVersion = '4.0.0'
    FormatsToProcess  = 'PSGet.Format.ps1xml'
    CmdletsToExport = @(
        'Find-PSResource',
        'Get-InstalledPSResource',
        'Get-PSResourceRepository',
        'Get-PSScriptFileInfo',
        'Install-PSResource',
        'Register-PSResourceRepository',
        'Save-PSResource',
        'Set-PSResourceRepository',
        'New-PSScriptFileInfo',
        'Test-PSScriptFileInfo',
        'Update-PSScriptFileInfo',
        'Publish-PSResource',
        'Uninstall-PSResource',
        'Unregister-PSResourceRepository',
        'Update-PSModuleManifest',
        'Update-PSResource')

    VariablesToExport = 'PSGetPath'
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Prerelease = 'beta22'
            Tags = @('PackageManagement',
                'PSEdition_Desktop',
                'PSEdition_Core',
                'Linux',
                'Mac',
                'Windows')
            ProjectUri = 'https://go.microsoft.com/fwlink/?LinkId=828955'
            LicenseUri = 'https://go.microsoft.com/fwlink/?LinkId=829061'
            ReleaseNotes = @'
## 0.5.22-beta22

### Breaking Changes
- PowerShellGet is now PSResourceGet! (#1164)
- Update-PSScriptFile is now Update-PSScriptFileInfo (#1140)
- New-PSScriptFile is now New-PSScriptFileInfo (#1140)
- Update-ModuleManifest is now Update-PSModuleManifest (#1139)
- -Tags parameter changed to -Tag in New-PSScriptFile, Update-PSScriptFileInfo, and Update-ModuleManifest (#1123)
- Change the type of -InputObject from PSResource to PSResource[] for Install-PSResource, Save-PSResource, and Uninstall-PSResource (#1124)
- PSModulePath is no longer referenced when searching paths (#1154)

### New Features
- Support for Azure Artifacts, GitHub Packages, and Artifactory (#1167, #1180)

### Bug Fixes
- Filter out unlisted packages (#1172, #1161)
- Add paging for V3 server requests (#1170)
- Support for floating versions (#1117)
- Update, Save, and Install with wildcard gets the latest version within specified range (#1117)
- Add positonal parameter for -Path in Publish-PSResource (#1111)
- Uninstall-PSResource -WhatIf now shows version and path of package being uninstalled (#1116)
- Find returns packages from the highest priority repository only (#1155)
- Bug fix for PSCredentialInfo constructor (#1156)
- Bug fix for Install-PSResource -NoClobber parameter (#1121)
- Save-PSResource now searches through all repos when no repo is specified (#1125)
- Caching for improved performance in Uninstall-PSResource (#1175)
- Bug fix for parsing package tags from local repository (#1119) 

See change log (CHANGELOG.md) at https://github.com/PowerShell/PSResourceGet
'@
        }
    }

    HelpInfoUri       = 'https://aka.ms/powershellget-3.x'
}

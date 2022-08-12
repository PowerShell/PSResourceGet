# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

@{
    RootModule        = './netstandard2.0/PowerShellGet.dll'
    ModuleVersion     = '3.0.16'
    GUID              = '1d73a601-4a6c-43c5-ba3f-619b18bbb404'
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
        'Get-PSResource',
        'Get-PSResourceRepository',
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
        'Update-ModuleManifest',
        'Update-PSResource')

    VariablesToExport = 'PSGetPath'
    AliasesToExport = @('inmo', 'fimo', 'upmo', 'pumo')
    PrivateData = @{
        PSData = @{
            Prerelease = 'beta16'
            Tags = @('PackageManagement',
                'PSEdition_Desktop',
                'PSEdition_Core',
                'Linux',
                'Mac',
                'Windows')
            ProjectUri = 'https://go.microsoft.com/fwlink/?LinkId=828955'
            LicenseUri = 'https://go.microsoft.com/fwlink/?LinkId=829061'
            ReleaseNotes = @'
## 3.0.16-beta16

### Bug Fixes
- Update NuGet dependency packages for security vulnerabilities (#733)

## 3.0.15-beta15

### New Features
- Implementation of New-ScriptFileInfo, Update-ScriptFileInfo, and Test-ScriptFileInfo cmdlets (#708)
- Implementation of Update-ModuleManifest cmdlet (#677)
- Implentation of Authenticode validation via -AuthenticodeCheck for Install-PSResource (#632)

### Bug Fixes
- Bug fix for installing modules with manifests that contain dynamic script blocks (#681)

## 3.0.14-beta14

### Bug Fixes
- Bug fix for repository store (#661)

## 3.0.13-beta

### New Features
- Implementation of -RequiredResourceFile and -RequiredResource parameters for Install-PSResource (#610, #592)
- Scope parameters for Get-PSResource and Uninstall-PSResource (#639)
- Support for credential persistence (#480 Thanks @cansuerdogan!)

### Bug Fixes
- Bug fix for publishing scripts (#642)
- Bug fix for publishing modules with 'RequiredModules' specified in the module manifest (#640)

### Changes
- 'SupportsWildcard' attribute added to Find-PSResource, Get-PSResource, Get-PSResourceRepository, Uninstall-PSResource, and Update-PSResource (#658)
- Updated help documentation (#651)
- -Repositories parameter changed to singular -Repository in Register-PSResource and Set-PSResource (#645)
- Better prerelease support for Uninstall-PSResource (#593)
- Rename PSResourceInfo's PrereleaseLabel property to match Prerelease column displayed (#591)
- Renaming of parameters -Url to -Uri (#551 Thanks @fsackur!)

See change log (CHANGELOG.md) at https://github.com/PowerShell/PowerShellGet
'@
        }
    }

    HelpInfoUri       = 'http://go.microsoft.com/fwlink/?linkid=855963'
}

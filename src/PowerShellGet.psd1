# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

@{
    RootModule        = './netstandard2.0/PowerShellGet.dll'
    ModuleVersion     = '3.0.17'
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
        'Find-ACR',
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
            Prerelease = 'beta17'
            Tags = @('PackageManagement',
                'PSEdition_Desktop',
                'PSEdition_Core',
                'Linux',
                'Mac',
                'Windows')
            ProjectUri = 'https://go.microsoft.com/fwlink/?LinkId=828955'
            LicenseUri = 'https://go.microsoft.com/fwlink/?LinkId=829061'
            ReleaseNotes = @'
## 3.0.17-beta17

### New Features
- Add -TemporaryPath parameter to Install-PSResource, Save-PSResource, and Update-PSResource (#763)
- Add String and SecureString as credential types in PSCredentialInfo (#764)
- Add a warning for when the script installation path is not in Path variable (#750)
- Expand acceptable paths for Publish-PSResource (Module root directory, module manifest file, script file)(#704)
- Add -Force parameter to Register-PSResourceRepository cmdlet, to override an existing repository (#717)

### Bug Fixes
- Change casing of -IncludeXML to -IncludeXml (#739)
- Update priority range for PSResourceRepository to 0-100 (#741)
- Editorial pass on cmdlet reference (#743)
- Fix issue when PSScriptInfo has no empty lines (#744)
- Make ConfirmImpact low for Register-PSResourceRepository and Save-PSResource (#745)
- Fix -PassThru for Set-PSResourceRepository cmdlet to return all properties (#748)
- Rename -FilePath parameter to -Path for PSScriptFileInfo cmdlets (#765)
- Fix RequiredModules description and add Find example to docs (#769)
- Remove unneeded inheritance in InstallHelper.cs (#773)
- Make -Path a required parameter for Save-PSResource cmdlet (#780)
- Improve script validation for publishing and installing (#781)

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

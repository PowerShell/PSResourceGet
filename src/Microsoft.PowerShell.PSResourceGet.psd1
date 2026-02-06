# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

@{
    RootModule             = './Microsoft.PowerShell.PSResourceGet.dll'
    NestedModules          = @('./Microsoft.PowerShell.PSResourceGet.psm1')
    ModuleVersion          = '1.2.0'
    CompatiblePSEditions   = @('Core', 'Desktop')
    GUID                   = 'e4e0bda1-0703-44a5-b70d-8fe704cd0643'
    Author                 = 'Microsoft Corporation'
    CompanyName            = 'Microsoft Corporation'
    Copyright              = '(c) Microsoft Corporation. All rights reserved.'
    Description            = 'PowerShell module with commands for discovering, installing, updating and publishing the PowerShell artifacts like Modules, Scripts, and DSC Resources.'
    PowerShellVersion      = '5.1'
    DotNetFrameworkVersion = '2.0'
    CLRVersion             = '4.0.0'
    FormatsToProcess       = 'PSGet.Format.ps1xml'
    CmdletsToExport        = @(
        'Compress-PSResource',
        'Find-PSResource',
        'Get-InstalledPSResource',
        'Get-PSResourceRepository',
        'Get-PSScriptFileInfo',
        'Install-PSResource',
        'Register-PSResourceRepository',
        'Reset-PSResourceRepository',
        'Save-PSResource',
        'Set-PSResourceRepository',
        'New-PSScriptFileInfo',
        'Test-PSScriptFileInfo',
        'Update-PSScriptFileInfo',
        'Publish-PSResource',
        'Uninstall-PSResource',
        'Unregister-PSResourceRepository',
        'Update-PSModuleManifest',
        'Update-PSResource'
    )
    FunctionsToExport      = @(
        'Import-PSGetRepository'
    )
    VariablesToExport = 'PSGetPath'
    AliasesToExport = @(
        'Get-PSResource',
        'fdres',
        'gres',
        'isres',
        'pbres',
        'svres',
        'udres',
        'usres')
    PrivateData = @{
        PSData = @{
            Prerelease   = 'rc2'
            Tags         = @('PackageManagement',
                'PSEdition_Desktop',
                'PSEdition_Core',
                'Linux',
                'Mac',
                'Windows')
            ProjectUri   = 'https://go.microsoft.com/fwlink/?LinkId=828955'
            LicenseUri   = 'https://go.microsoft.com/fwlink/?LinkId=829061'
            ReleaseNotes = @'
## 1.2.0-rc2

## Bug fix
- For packages with dependency on a specific version use specific version instead of version range (#1937)

## 1.2.0-rc1

## Bug fix
- `WhatIf` parameter should respect provided value instead of simply checking presence (#1925)

## 1.2.0-preview5

### New Features
- Add `Reset-PSResourceRepository` cmdlet to recover from corrupted repository store (#1895)
- Improve performance of `ContainerRegistry` repositories by caching token (#1920)

## Bug fix
- Ensure `Update-PSResource` does not re-install dependency packages which already satisfy dependency criteria (#1919)
- Retrieve non-anonymous access token when publishing to ACR (#1918)
- Filter out path separators when passing in package names as a parameter for any cmdlet (#1916)
- Respect `TrustRepository` parameter when using `-RequiredResource` with `Install-PSResource` (#1910)
- Fix bug with 'PSModuleInfo' property deserialization when validating module manifest (#1909) 
- Prevent users from setting ApiVersion to 'Unknown' in `Set-PSResourceRepository` and `Register-PSResourceRepository` (#1892)

## 1.2.0-preview4

## Bug fix

- Fix typos in numerous files (#1875 Thanks @SamErde!)
- MAR fails to parse RequiredVersion for dependencies (#1876 Thanks @o-l-a-v!)
- Get-InstalledPSResource -Path don't throw if no subdirectories were found (#1877 Thanks @o-l-a-v!)
- Handle boolean correctly in RequiredResourceFile for prerelease key (#1843 Thanks @o-l-a-v!)
- Fix CodeQL configuration (#1886)
- Add cmdlet aliases: gres, usres, and svres (#1888)
- Add warning when AuthenticodeCheck is used on non-Windows platforms (#1891)
- Fix Compress-PSResource ignoring .gitkeep and other dotfiles (#1889)
- Add CodeQL suppression for ContainerRegistryServerAPICalls (#1897)
- Fix broken Install-PSResource test with warning condition incorrect (#1899)
- Uninstall-PSResource should not fail silently when resource was not found or prerelease criteria not met (#1898)
- Uninstall-PSResource should delete subdirectories without Access Denied error on OneDrive (#1860)

## 1.2.0-preview3

### New Features
- Pagination for MCR catalog items (#1870)

### Bug Fix
- Bug fix for CLM issues (#1869)
- Update `-ModulePrefix` to be a static parameter (#1868)
- Bug fix for populating all `#Requires` fields in `Update-PSScriptFileInfo` (#1863)
- Bug fix for populating `Includes` metadata for packages from container registry repositories (#1861)
- Bug fix for `Find-PSResource` and `Install-PSResource` not retrieving unlisted package versions (#1859)

## 1.2.0-preview2

### New Features
- Integration of the Azure Artifacts Credential Provider for ADO feeds (#1765)

### Bug Fix
- Bug fixes for NuGet v3 dependencies (#1841 Thanks @o-l-a-v!)
- Bug fix for temporary installation path failure when installing PSResources on Linux machines (#1842 Thanks @o-l-a-v!)

## 1.2.0-preview1

### New Features
- Dependency support for PSResources in v3 repositories (#1778 Thanks @o-l-a-v!)

### Bug Fix
- Updated dependencies and added connection timeout to improve CI tests reliability (#1829)
- Improvements in `ContainerRegistry` repositories in listing repository catalog  (#1831)
- Wildcard attribute added to `-Repository` parameter of `Install-PSResource` (#1808)

See change log (CHANGELOG) at https://github.com/PowerShell/PSResourceGet
'@
        }
    }

    HelpInfoUri            = 'https://go.microsoft.com/fwlink/?linkid=2238183'
}

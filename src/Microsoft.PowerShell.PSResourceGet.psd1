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
        'isres',
        'pbres',
        'udres')
    PrivateData = @{
        PSData = @{
            Prerelease   = 'preview2'
            Tags         = @('PackageManagement',
                'PSEdition_Desktop',
                'PSEdition_Core',
                'Linux',
                'Mac',
                'Windows')
            ProjectUri   = 'https://go.microsoft.com/fwlink/?LinkId=828955'
            LicenseUri   = 'https://go.microsoft.com/fwlink/?LinkId=829061'
            ReleaseNotes = @'
## 1.2.0-preview2

### New Features
- Ingetration of the Azure Artifacts Credential Provider for ADO feeds (#1765)

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

## 1.1.1

### Bug Fix
- Bugfix to retrieve all metadata properties when finding a PSResource from a ContainerRegistry repository (#1799)
- Update README.md (#1798)
- Use authentication challenge for unauthenticated ContainerRegistry repository (#1797)
- Bugfix for Install-PSResource with varying digit version against ContainerRegistry repository (#1796)
- Bugfix for updating ContainerRegistry dependency parsing logic to account for AzPreview package (#1792)
- Add wildcard support for MAR repository for FindAll and FindByName (#1786)
- Bugfix for nuspec dependency version range calculation for RequiredModules (#1784)

## 1.1.0

### Bug Fix
- Bugfix for publishing .nupkg file to ContainerRegistry repository (#1763)
- Bugfix for PMPs like Artifactory needing modified filter query parameter to proxy upstream (#1761)
- Bugfix for ContainerRegistry repository to parse out dependencies from metadata (#1766)
- Bugfix for Install-PSResource Null pointer occurring when package is present only in upstream feed in ADO (#1760)
- Bugfix for local repository casing issue on Linux (#1750)
- Update README.md (#1759)
- Bug fix for case sensitive License.txt when RequireLicense is specified (#1757)
- Bug fix for broken -Quiet parameter for Save-PSResource (#1745)

## 1.1.0-rc3

### Bug Fix
- Include missing commits

## 1.1.0-RC2

### New Features
- Full Microsoft Artifact Registry integration (#1741)

### Bug Fixes

- Update to use OCI v2 APIs for Container Registry (#1737)
- Bug fixes for finding and installing from local repositories on Linux machines (#1738)
- Bug fix for finding package name with 4 part version from local repositories (#1739) 

## 1.1.0-RC1

### New Features

- Group Policy configurations for enabling or disabling PSResource repositories (#1730)

### Bug Fixes

- Fix packaging name matching when searching in local repositories (#1731)
- `Compress-PSResource` `-PassThru` now passes `FileInfo` instead of string (#1720)
- Fix for `Compress-PSResource` not properly compressing scripts  (#1719) 
- Add `AcceptLicense` to Save-PSResource (#1718 Thanks @o-l-a-v!)
- Better support for NuGet v2 feeds (#1713 Thanks @o-l-a-v!)
- Better handling of `-WhatIf` support in `Install-PSResource` (#1531 Thanks @o-l-a-v!)
- Fix for some nupkgs failing to extract due to empty directories (#1707 Thanks @o-l-a-v!)
- Fix for searching for `-Name *` in `Find-PSResource` (#1706 Thanks @o-l-a-v!)

## 1.1.0-preview2

### New Features

- New cmdlet `Compress-PSResource` which packs a package into a .nupkg and saves it to the file system (#1682, #1702)
- New `-Nupkg` parameter for `Publish-PSResource` which pushes pushes a .nupkg to a repository (#1682)
- New `-ModulePrefix` parameter for `Publish-PSResource` which adds a prefix to a module name for container registry repositories to add a module prefix.This is only used for publishing and is not part of metadata. MAR will drop the prefix when syndicating from ACR to MAR (#1694)

### Bug Fixes

- Add prerelease string when NormalizedVersion doesn't exist, but prelease string does (#1681 Thanks @sean-r-williams)
- Add retry logic when deleting files (#1667 Thanks @o-l-a-v!)
- Fix broken PAT token use (#1672)
- Updated error messaging for authenticode signature failures (#1701)

## 1.1.0-preview1

### New Features

- Support for Azure Container Registries (#1495, #1497-#1499, #1501, #1502, #1505, #1522, #1545, #1548, #1550, #1554, #1560, #1567, 
#1573, #1576, #1587, #1588, #1589, #1594, #1598, #1600, #1602, #1604, #1615)

### Bug Fixes

- Fix incorrect request URL when installing resources from ADO (#1597 Thanks @anytonyoni!)
- Fix for swallowed exceptions (#1569)
- Fix for PSResourceGet not working in Constrained Languange Mode (#1564)

## 1.0.6

- Bump System.Text.Json to 8.0.5

## [1.0.5](https://github.com/PowerShell/PSResourceGet/compare/v1.0.4.1...v1.0.5) - 2024-05-13

### Bug Fixes
- Update `nuget.config` to use PowerShell packages feed (#1649)
- Refactor V2ServerAPICalls and NuGetServerAPICalls to use object-oriented query/filter builder (#1645 Thanks @sean-r-williams!)
- Fix unnecessary `and` for version globbing in V2ServerAPICalls (#1644 Thanks again @sean-r-williams!)
- Fix requiring `tags` in server response (#1627 Thanks @evelyn-bi!)
- Add 10 minute timeout to HTTPClient (#1626)
- Fix save script without `-IncludeXml` (#1609, #1614 Thanks @o-l-a-v!)
- PAT token fix to translate into HttpClient 'Basic Authorization'(#1599 Thanks @gerryleys!)
- Fix incorrect request url when installing from ADO (#1597 Thanks @antonyoni!)
- Improved exception handling (#1569)
- Ensure that .NET methods are not called in order to enable use in Constrained Language Mode (#1564)
- PSResourceGet packaging update

## [1.0.4.1](https://github.com/PowerShell/PSResourceGet/compare/v1.0.4...v1.0.4.1) - 2024-04-05

- PSResourceGet packaging update

## [1.0.4](https://github.com/PowerShell/PSResourceGet/compare/v1.0.3...v1.0.4) - 2024-04-05

### Patch

- Dependency package updates

## 1.0.3

### Bug Fixes
- Bug fix for null package version in `Install-PSResource`

## 1.0.2

### Bug Fixes

- Bug fix for `Update-PSResource` not updating from correct repository (#1549)
- Bug fix for creating temp home directory on Unix (#1544)
- Bug fix for creating `InstalledScriptInfos` directory when it does not exist (#1542)
- Bug fix for `Update-ModuleManifest` throwing null pointer exception (#1538)
- Bug fix for `name` property not populating in `PSResourceInfo` object when using `Find-PSResource` with JFrog Artifactory (#1535)
- Bug fix for incorrect configuration of requests to JFrog Artifactory v2 endpoints (#1533 Thanks @sean-r-williams!)
- Bug fix for determining JFrog Artifactory repositories (#1532 Thanks @sean-r-williams!)
- Bug fix for v2 server repositories incorrectly adding script endpoint (1526)
- Bug fixes for null references (#1525)
- Typo fixes in message prompts in `Install-PSResource` (#1510 Thanks @NextGData!)
- Bug fix to add `NormalizedVersion` property to `AdditionalMetadata` only when it exists (#1503 Thanks @sean-r-williams!)
- Bug fix to verify whether `Uri` is a UNC path and set respective `ApiVersion` (#1479 Thanks @kborowinski!)

## 1.0.1

### Bug Fixes

- Bugfix to update Unix local user installation paths to be compatible with .NET 7 and .NET 8 (#1464)
- Bugfix for Import-PSGetRepository in Windows PowerShell (#1460)
- Bugfix for `Test-PSScriptFileInfo`` to be less sensitive to whitespace (#1457)
- Bugfix to overwrite rels/rels directory on net472 when extracting nupkg to directory (#1456)
- Bugfix to add pipeline by property name support for Name and Repository properties for Find-PSResource (#1451 Thanks @ThomasNieto!)

## 1.0.0

### New Features
- Add `ApiVersion` parameter for `Register-PSResourceRepository` (#1431)

### Bug Fixes
- Automatically set the ApiVersion to v2 for repositories imported from PowerShellGet (#1430)
- Bug fix ADO v2 feed installation failures (#1429)
- Bug fix Artifactory v2 endpoint failures (#1428)
- Bug fix Artifactory v3 endpoint failures (#1427)
- Bug fix `-RequiredResource` silent failures (#1426)
- Bug fix for v2 repository returning extra packages for `-Tag` based search with `-Prerelease` (#1405) 

See change log (CHANGELOG) at https://github.com/PowerShell/PSResourceGet
'@
        }
    }

    HelpInfoUri            = 'https://go.microsoft.com/fwlink/?linkid=2238183'
}

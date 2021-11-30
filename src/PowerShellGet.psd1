# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

@{
    RootModule        = './netstandard2.0/PowerShellGet.dll'
    ModuleVersion     = '3.0.12'
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
        'Publish-PSResource',
        'Uninstall-PSResource',
        'Unregister-PSResourceRepository',
        'Update-PSResource')

    VariablesToExport = 'PSGetPath'
    AliasesToExport = @('inmo', 'fimo', 'upmo', 'pumo')
    PrivateData = @{
        PSData = @{
            Prerelease = 'beta'
            Tags = @('PackageManagement',
                'PSEdition_Desktop',
                'PSEdition_Core',
                'Linux',
                'Mac',
                'Windows')
            ProjectUri = 'https://go.microsoft.com/fwlink/?LinkId=828955'
            LicenseUri = 'https://go.microsoft.com/fwlink/?LinkId=829061'
            ReleaseNotes = @'

### 3.0.11
In this release, all cmdlets have been reviewed and implementation code refactored as needed.
Cmdlets have most of their functionality, but some parameters are not yet implemented and will be added in future releases.
All tests have been reviewed and rewritten as needed.

- Graceful handling of paths that do not exist
- The repository store (PSResourceRepository.xml) is auto-generated if it does not already exist. It also automatically registers the PowerShellGallery with a default priority of 50 and a default trusted value of false. 
- Better Linux support, including graceful exits when paths do not exist
- Better pipeline input support all cmdlets
- General wildcard support for all cmdlets
- WhatIf support for all cmdlets
- All cmdlets output concrete return types
- Better help documentation for all cmdlets
- Using an exact prerelease version with Find, Install, or Save no longer requires `-Prerelease` tag
- Support for finding, installing, saving, and updating PowerShell resources from Azure Artifact feeds
- Publish-PSResource now properly dispays 'Tags' in nuspec
- Find-PSResource quickly cancels transactions with 'CTRL + C'
- Register-PSRepository now handles relative paths
- Find-PSResource and Save-PSResource deduplicates dependencies
- Install-PSResource no longer creates version folder with the prerelease tag
- Update-PSResource can now update all resources, and no longer requires name param
- Save-PSResource properly handles saving scripts
- Get-PSResource uses default PowerShell paths
'@
        }
    }

    HelpInfoURI       = 'http://go.microsoft.com/fwlink/?linkid=855963'
}

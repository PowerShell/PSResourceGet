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

### 3.0.12
In this release, all cmdlets have been reviewed and implementation code refactored as needed.
Missing implementation from the past prerelease has been completed for parameters and parameter sets.
High priority bugs which were either outstanding or from the last prerelease have also been addressed.
All tests have been reviewed and rewritten as needed.
Please note, that wildcard search for AzureDevOps feed repositories is not supported at this time.

- Support searching for all packages from a repository (i.e `Find-PSResource -Name “*”`). Note, wildcard search is not supported for AzureDevOps feed repositories and will write an error message accordingly).
- Packages found are now unique by Name,Version,Repository.
- Support searching for and returning packages found across multiple repositories when using wildcard with Repository parameter (i.e `Find-PSResource “PackageExistingInMultipleRepos” -Repository “*”` will perform an exhaustive search).
- Add consistent pipeline input support.
  - PSResourceInfo objects can be piped into: Install-PSResource, Uninstall-PSResource, Save-PSResource. PSRepositoryInfo objects can be piped into: Unregister-PSResourceRepository
- For more consistent pipeline support, the following cmdlets have pipeline support for the listed parameter(s):
  - Find-PSResource (Name param, ValueFromPipeline)
  - Get-PSResource (Name param, ValueFromPipeline)
  - Install-PSResource (Name param, ValueFromPipeline)
  - Publish-PSResource (None)
  - Save-PSResource (Name param, ValueFromPipeline)
  - Uninstall-PSResource (Name param, ValueFromPipeline)
  - Update-PSResource (Name param, ValueFromPipeline)
  - Get-PSResourceRepository (Name param, ValueFromPipeline)
  - Set-PSResourceRepository (Name param, ValueFromPipeline)
  - Register-PSResourceRepository (None)
  - Unregister-PSResourceRepository (Name param, ValueFromPipelineByPropertyName)
- Implement `-Tag` parameter set for Find-PSResource (i.e `Find-PSResource -Tag “JSON”`)
- Implement `-Type` parameter set for Find-PSResource (i.e `Find-PSResource -Type Module`)
- Implement CommandName and DSCResourceName parameter sets for Find-PSResource (i.e Find-PSResource -CommandName “Get-TargetResource”).
- Add consistent pre-release version support for cmdlets, including Uninstall-PSResource and Get-PSResource. For example, running `Get-PSResource “MyPackage” -Version “2.0.0-beta”` would only return MyPackage with version “2.0.0” and prerelease “beta”, NOT MyPackage with version “2.0.0.0” (i.e a stable version).
- Add progress bar for installation completion for Install-PSResource, Update-PSResource and Save-PSResource.
- Implement `-Quiet` param for Install-PSResource and Update-PSResource. This suppresses the progress bar display when passed in.
- Implement `-PassThru` parameter for all appropriate cmdlets. Install-PSResource, Save-PSResource, Update-PSResource and Unregister-PSResourceRepository cmdlets now have `-PassThru` support thus completing this goal.
- Implement `-SkipDependencies` parameter for Install-PSResource, Save-PSResource, and Update-PSResource cmdlets.
- Implement `-AsNupkg` and `-IncludeXML` parameters for Save-PSResource.
- Implement `-DestinationPath` parameter for Publish-PSResource
- Add `-NoClobber` functionality to Install-PSResource.
- Add thorough error handling to Update-PSResource to cover more cases and gracefully write errors when updates can’t be performed.
- Add thorough error handling to Install-PSResource to cover more cases and not fail silently when installation could not happen successfully. Also fixes bug where package would install even if it was already installed and `-Reinstall` parameter was not specified.
- Restore package if installation attempt fails when reinstalling a package.
- Fix bug with some Modules installing as Scripts.
- Fix bug with separating `$env:PSModulePath` to now work with path separators across all OS systems including Unix.
- Fix bug to register repositories with local file share paths, ensuring repositories with valid URIs can be registered.
- Revert cmdlet name ‘Get-InstalledPSResource’ to ‘Get-PSResource’
- Remove DSCResources from PowerShellGet.
- Remove unnecessary assemblies.
'@
        }
    }

    HelpInfoURI       = 'http://go.microsoft.com/fwlink/?linkid=855963'
}

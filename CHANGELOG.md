# CHANGELOG

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

## 3.0.12-beta

### Changes
- Support searching for all packages from a repository (i.e 'Find-PSResource -Name '*''). Note, wildcard search is not supported for AzureDevOps feed repositories and will write an error message accordingly).
- Packages found are now unique by Name,Version,Repository.
- Support searching for and returning packages found across multiple repositories when using wildcard with Repository parameter (i.e 'Find-PSResource -Name 'PackageExistingInMultipleRepos' -Repository '*'' will perform an exhaustive search).
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
- Implement '-Tag' parameter set for Find-PSResource (i.e 'Find-PSResource -Tag 'JSON'')
- Implement '-Type' parameter set for Find-PSResource (i.e 'Find-PSResource -Type Module')
- Implement CommandName and DSCResourceName parameter sets for Find-PSResource (i.e Find-PSResource -CommandName "Get-TargetResource").
- Add consistent pre-release version support for cmdlets, including Uninstall-PSResource and Get-PSResource. For example, running 'Get-PSResource 'MyPackage' -Version '2.0.0-beta'' would only return MyPackage with version "2.0.0" and prerelease "beta", NOT MyPackage with version "2.0.0.0" (i.e a stable version).
- Add progress bar for installation completion for Install-PSResource, Update-PSResource and Save-PSResource.
- Implement '-Quiet' param for Install-PSResource, Save-PSResource and Update-PSResource. This suppresses the progress bar display when passed in.
- Implement '-PassThru' parameter for all appropriate cmdlets. Install-PSResource, Save-PSResource, Update-PSResource and Unregister-PSResourceRepository cmdlets now have '-PassThru' support thus completing this goal.
- Implement '-SkipDependencies' parameter for Install-PSResource, Save-PSResource, and Update-PSResource cmdlets.
- Implement '-AsNupkg' and '-IncludeXML' parameters for Save-PSResource.
- Implement '-DestinationPath' parameter for Publish-PSResource
- Add '-NoClobber' functionality to Install-PSResource.
- Add thorough error handling to Update-PSResource to cover more cases and gracefully write errors when updates can't be performed.
- Add thorough error handling to Install-PSResource to cover more cases and not fail silently when installation could not happen successfully. Also fixes bug where package would install even if it was already installed and '-Reinstall' parameter was not specified.
- Restore package if installation attempt fails when reinstalling a package.
- Fix bug with some Modules installing as Scripts.
- Fix bug with separating '$env:PSModulePath' to now work with path separators across all OS systems including Unix.
- Fix bug to register repositories with local file share paths, ensuring repositories with valid URIs can be registered.
- Revert cmdlet name 'Get-InstalledPSResource' to 'Get-PSResource'
- Remove DSCResources from PowerShellGet.
- Remove unnecessary assemblies.

## 3.0.11-beta

### Changes
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
- Get-InstalledPSResource uses default PowerShell paths


### Notes
In this release, all cmdlets have been reviewed and implementation code refactored as needed.
Cmdlets have most of their functionality, but some parameters are not yet implemented and will be added in future releases.
All tests have been reviewed and rewritten as needed.


## 3.0.0-beta10
Bug Fixes
* Bug fix for -ModuleName (used with -Version) in Find-PSResource returning incorrect resource type
* Make repositories unique by name
* Add tab completion for -Name parameter in Get-PSResource, Set-PSResource, and Unregister-PSResource
* Remove credential argument from Register-PSResourceRepository
* Change returned version type from 'NuGet.Version' to 'System.Version'
* Have Install output verbose message on successful installation (error for unsuccessful installation)
* Ensure that not passing credentials does not throw an error if searching through multiple repositories
* Remove attempt to remove loaded assemblies in psm1

## 3.0.0-beta9
New Features
* Add DSCResources

Bug Fixes
* Fix bug related to finding dependencies that do not have a specified version in Find-PSResource
* Fix bug related to parsing 'RequiredModules' in .psd1 in Publish-PSResource
* Improve error handling for when repository in Publish-PSResource does not exist
* Fix for unix paths in Get-PSResource, Install-PSResource, and Uninstall-PSResource
* Add debugging statements for Get-PSResource and Install-PSResource
* Fix bug related to paths in Uninstall-PSResource

## 3.0.0-beta8
New Features 
* Add Type parameter to Install-PSResource
* Add 'sudo' check for admin privileges in Unix in Install-PSResource

Bug Fixes
* Fix bug with retrieving installed scripts in Get-PSResource
* Fix bug with AllUsers scope in Windows in Install-PSResource
* Fix bug with Uninstall-PSResource sometimes not fully uninstalling
* Change installed file paths to contain original version number instead of normalized version

## 3.0.0-beta7
New Features 
* Completed functionality for Update-PSResource
* Input-Object parameter for Install-PSResource

Bug Fixes
* Improved experience when loading module for diffent frameworks
* Bug fix for assembly loading error in Publish-PSResource
* Allow for relative paths when registering psrepository
* Improved error handling for Install-PSResource and Update-PSResource
* Remove prerelease tag from module version directory
* Fix error getting thrown from paths with incorrectly formatted module versions
* Fix module installation paths on Linux and MacOS

## 3.0.0-beta6
New Feature 
* Implement functionality for Publish-PSResource

## 3.0.0-beta5
* Note: 3.0.0-beta5 was skipped due to a packaging error

## 3.0.0-beta4
New Feature
* Implement -Repository '*' in Find-PSResource to search through all repositories instead of prioritized repository 

Bug Fix
* Fix poor error handling for when repository is not accessible in Find-PSResource

## 3.0.0-beta3
New Features
* -RequiredResource parameter for Install-PSResource 
* -RequiredResourceFile parameter for Install-PSResource
* -IncludeXML parameter in Save-PSResource

Bug Fixes
* Resolved paths in Install-PSRsource and Save-PSResource 
* Resolved issues with capitalization (for unix systems) in Install-PSResource and Save-PSResource

## 3.0.0-beta2
New Features
* Progress bar and -Quiet parameter for Install-PSResource
* -TrustRepository parameter for Install-PSResource
* -NoClobber parameter for Install-PSResource
* -AcceptLicense for Install-PSResource
* -Force parameter for Install-PSResource
* -Reinstall parameter for Install-PSResource
* Improved error handling

## 3.0.0-beta1
BREAKING CHANGE
* Preview version of PowerShellGet. Many features are not fully implemented yet. Please see https://devblogs.microsoft.com/powershell/powershellget-3-0-preview1 for more details.
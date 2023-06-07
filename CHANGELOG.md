# CHANGELOG
## 0.5.22-beta22

### Breaking Changes
- PowerShellGet is now PSResourceGet! (#1164)
- Update-PSScriptFile is now Update-PSScriptFileInfo (#1140)
- New-PSScriptFile is now New-PSScriptFileInfo (#1140)
- Update-ModuleManifest is now Update-PSModuleManifest (#1139)
- -Tags parameter changed to -Tag in New-PSScriptFile, Update-PSScriptFileInfo, and Update-ModuleManifest (#1123)
- Change the type of -InputObject from PSResourceInfo to PSResourceInfo[] for Install-PSResource, Save-PSResource, and Uninstall-PSResource (#1124)

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

## 3.0.21-beta21

### New Features
- Move off of NuGet client APIs for local repositories (#1065)

### Bug Fixes
- Update properties on PSResourceInfo object (#1077)
- Rename PSScriptFileInfo and Get-PSResource cmdlets (#1071)
- fix ValueFromPipelineByPropertyName on Save, Install (#1070)
- add Help message for mandatory params across cmdlets (#1068)
- fix version range bug for Update-PSResource (#1067)
- Fix attribute bugfixes for Find and Install params (#1066)
- Correct Unexpected spelling of Unexpected (#1059)
- Resolve bug with Find-PSResource -Type Module not returning modules (#1050)
- Inject credentials to ISettings to pass them into PushRunner (#993)

## 3.0.20-beta20

- Move off of NuGet client APIs and use direct REST API calls for remote repositories (#1023)


### Bug Fixes
- Updates to dependency installation (#1010) (#996) (#907)
- Update to retrieving all packages installed on machine (#999)
- PSResourceInfo version correctly displays 2 or 3 digit version numbers (#697)
- Using `Find-PSresource` with `-CommandName` or `-DSCResourceName` parameters returns an object with a properly expanded ParentResource member (#754)
- `Find-PSResource` no longer returns duplicate results (#755)
- `Find-PSResource` lists repository 'PSGalleryScripts' which does not exist for `Get-PSResourceRepository` (#1028)

## 3.0.19-beta19

### New Features
- Add `-SkipModuleManifestValidate` parameter to `Publish-PSResource` (#904)

### Bug Fixes
- Add new parameter sets for `-IncludeXml` and `-AsNupkg` parameters in `Install-PSResource` (#910)
- Change warning to error in `Update-PSResource` when no module is already installed (#909)
- Fix `-NoClobber` bug throwing error in `Install-PSResource` (#908)
- Remove warning when installing dependencies (#907)
- Remove Proxy parameters from `Register-PSResourceRepository` (#906)
- Remove -PassThru parameter from `Update-ModuleManifest` (#900)

## 3.0.18-beta18

### New Features
- Add Get-PSScriptFileInfo cmdlet (#839)
- Allow CredentialInfo parameter to accept a hashtable  (#836)

### Bug Fixes
- Publish-PSResource now preserves folder and file structure (#882)
- Fix verbose message for untrusted repos gaining trust (#841)
- Fix for Update-PSResource attempting to reinstall latest preview version (#834)
- Add SupportsWildcards() attribute to parameters accepting wildcards (#833)
- Perform Repository trust check when installing a package (#831)
- Fix casing of `PSResource` in `Install-PSResource` (#820)
- Update .nuspec 'license' property to 'licenseUrl' (#850)

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

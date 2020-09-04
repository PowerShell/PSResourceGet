### 3.0.0-beta10
# Changelog
Bug Fixes
* Bug fix for -ModuleName (used with -Version) in Find-PSResource returning incorrect resource type
* Make repositories unique by name
* Add tab completion for -Name parameter in Get-PSResource, Set-PSResource, and Unregister-PSResource
* Remove credential argument from Register-PSResourceRepository
* Change returned version type from 'NuGet.Version' to 'System.Version'
* Have Install output verbose message on successful installation (error for unsuccessful installation)
* Ensure that not passing credentials does not throw an error if searching through multiple repositories
* Remove attempt to remove loaded assemblies in psm1

### 3.0.0-beta9
# Changelog
New Features
* Add DSCResources

Bug Fixes
* Fix bug related to finding dependencies that do not have a specified version in Find-PSResource
* Fix bug related to parsing 'RequiredModules' in .psd1 in Publish-PSResource
* Improve error handling for when repository in Publish-PSResource does not exist
* Fix for unix paths in Get-PSResource, Install-PSResource, and Uninstall-PSResource
* Add debugging statements for Get-PSResource and Install-PSResource
* Fix bug related to paths in Uninstall-PSResource

### 3.0.0-beta8
New Features 
* Add Type parameter to Install-PSResource
* Add 'sudo' check for admin privileges in Unix in Install-PSResource

Bug Fixes
* Fix bug with retrieving installed scripts in Get-PSResource
* Fix bug with AllUsers scope in Windows in Install-PSResource
* Fix bug with Uninstall-PSResource sometimes not fully uninstalling
* Change installed file paths to contain original version number instead of normalized version

### 3.0.0-beta7
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

### 3.0.0-beta6
New Feature 
* Implement functionality for Publish-PSResource

### 3.0.0-beta5
* Note: 3.0.0-beta5 was skipped due to a packaging error

### 3.0.0-beta4
New Feature
* Implement -Repository '*' in Find-PSResource to search through all repositories instead of prioritized repository 

Bug Fix
* Fix poor error handling for when repository is not accessible in Find-PSResource

### 3.0.0-beta3
New Features
* -RequiredResource parameter for Install-PSResource 
* -RequiredResourceFile parameter for Install-PSResource
* -IncludeXML parameter in Save-PSResource

Bug Fixes
* Resolved paths in Install-PSRsource and Save-PSResource 
* Resolved issues with capitalization (for unix systems) in Install-PSResource and Save-PSResource

### 3.0.0-beta2
New Features
* Progress bar and -Quiet parameter for Install-PSResource
* -TrustRepository parameter for Install-PSResource
* -NoClobber parameter for Install-PSResource
* -AcceptLicense for Install-PSResource
* -Force parameter for Install-PSResource
* -Reinstall parameter for Install-PSResource
* Improved error handling

### 3.0.0-beta1
BREAKING CHANGE
* Preview version of PowerShellGet. Many features are not fully implemented yet. Please see https://devblogs.microsoft.com/powershell/powershellget-3-0-preview1 for more details.
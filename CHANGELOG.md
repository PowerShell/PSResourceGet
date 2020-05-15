# Changelog

### 3.0.0-beta3
New Features
* -RequiredResource parameter for Install-PSResource 
* -RequiredResourceFile parameter for Install-PSResource
* -AsNupkg parameter in Save-PSResource

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
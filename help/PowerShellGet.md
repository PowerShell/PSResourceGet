---
Download Help Link: https://aka.ms/powershell73-help
Help Version: 3.0.16
Locale: en-US
Module Guid: 1d73a601-4a6c-43c5-ba3f-619b18bbb404
Module Name: PowerShellGet
ms.date: 08/03/2022
---

# PowerShellGet Module
## Description
PowerShellGet is a module with commands for discovering, installing, updating and publishing
PowerShell artifacts like Modules, DSC Resources, Role Capabilities, and Scripts.

This documentation covers the latest preview version PowerShellGet v3.

## PowerShellGet Cmdlets
### [Find-PSResource](Find-PSResource.md)
Searches for packages from a repository (local or remote), based on a name or other package properties.

### [Get-PSResource](Get-PSResource.md)
Returns modules and scripts installed on the machine via **PowerShellGet**.

### [Get-PSResourceRepository](Get-PSResourceRepository.md)
Finds and returns registered repository information.

### [Install-PSResource](Install-PSResource.md)
Installs resources from a registered repository.

### [New-PSScriptFileInfo](New-PSScriptFileInfo.md)
The cmdlet creates a new script file, including metadata about the script.

### [PowerShellGet](PowerShellGet.md)
{{ Fill in the Description }}

### [Publish-PSResource](Publish-PSResource.md)
Publishes a specified module from the local computer to PSResource repository.

### [Register-PSResourceRepository](Register-PSResourceRepository.md)
Registers a repository for PowerShell resources.

### [Save-PSResource](Save-PSResource.md)
Saves resources (modules and scripts) from a registered repository onto the machine.

### [Set-PSResourceRepository](Set-PSResourceRepository.md)
Sets information for a registered repository.

### [Test-PSScriptFileInfo](Test-PSScriptFileInfo.md)
Tests the comment-based metadata in a `.ps1` file to ensure it's valid for publication.

### [Uninstall-PSResource](Uninstall-PSResource.md)
Uninstalls a resource that was installed using **PowerShellGet**.

### [Unregister-PSResourceRepository](Unregister-PSResourceRepository.md)
Removes a registered repository from the local machine.

### [Update-ModuleManifest](Update-ModuleManifest.md)
Updates a module manifest file.

### [Update-PSResource](Update-PSResource.md)
Downloads and installs the newest version of a package already installed on the local machine.

### [Update-PSScriptFileInfo](Update-PSScriptFileInfo.md)
This cmdlet updates the comment-based metadata in an existing script `.ps1` file.

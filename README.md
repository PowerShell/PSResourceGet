
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/PowerShell/PowerShellGet/blob/development/LICENSE)
[![Documentation - PowerShellGet](https://img.shields.io/badge/Documentation-PowerShellGet-blue.svg)](https://docs.microsoft.com/en-us/powershell/module/powershellget/?view=powershell-7.1)
[![PowerShell Gallery - PowerShellGet](https://img.shields.io/badge/PowerShell%20Gallery-PowerShellGet-blue.svg)](https://www.powershellgallery.com/packages/PowerShellGet)
[![Minimum Supported PowerShell Version](https://img.shields.io/badge/PowerShell-5.0-blue.svg)](https://github.com/PowerShell/PowerShellGet)

Important Note
==============
This version of PowerShellGet is currently under development and is not feature complete.
As a result, we are currently only accepting PRs for tests.
If you would like to open a PR please open an issue first so that necessary discussion can take place.
Please open an issue for any feature requests, bug reports, or questions for PowerShellGet version 3.0 (currently available as a preview release).
Please note, the repository for previous versions of PowerShellGet has a new location at [PowerShell/PowerShellGetv2](https://github.com/PowerShell/PowerShellGetv2).  

Introduction
============

PowerShellGet is a PowerShell module with commands for discovering, installing, updating and publishing the PowerShell artifacts like Modules, Scripts, and DSC Resources.  

Documentation
=============

Documentation for PowerShellGet 3.0 has not yet been published, please
[Click here](https://docs.microsoft.com/powershell/module/PowerShellGet/?view=powershell-7)
to reference the documentation for previous versions of PowerShellGet.  

Requirements
============

- PowerShell 5.0 or higher.

Get PowerShellGet Module
========================

Please refer to our [documentation](https://www.powershellgallery.com/packages/PowerShellGet/) for the up-to-date version on how to get the PowerShellGet Module.

Get PowerShellGet Source
========================

#### Steps

* Obtain the source
    - Download the latest source code from the release page (https://github.com/PowerShell/PowerShellGet/releases) OR
    - Clone the repository (needs git)
    ```powershell
    git clone https://github.com/PowerShell/PowerShellGet
    ```

* Navigate to the local repository directory

```powershell
PS C:\> cd c:\Repos\PowerShellGet
PS C:\Repos\PowerShellGet>
```

* Install PSPackageProject module if needed

```powershell
if ((Get-Module -Name PSPackageProject -ListAvailable).Count -eq 0) {
    Install-Module -Name PSPackageProject -Repository PSGallery
}
```

* Build the project

```powershell
# Build for the netstandard2.0 framework
PS C:\Repos\PowerShellGet> .\build.ps1 -Clean -Build -BuildConfiguration Debug -BuildFramework netstandard2.0
```

* Publish the module to a local repository

```powershell
PS C:\Repos\PowerShellGet> .\build.ps1 -Publish
```

* Run functional tests

```powershell
PS C:\Repos\PowerShellGet> Invoke-PSPackageProjectTest -Type Functional
```

* Import the module into a new PowerShell session

```powershell
# If running PowerShell 6+
C:\> Import-Module C:\Repos\PowerShellGet\out\PowerShellGet

# If running Windows PowerShell
C:\> Import-Module C:\Repos\PowerShellGet\out\PowerShellGet\PowerShellGet.psd1
```

**Note**  
PowerShellGet consists of .NET binaries and so can be imported into a PowerShell session only once.
Since the PSPackageProject module, used to build the module, has a dependency on earlier versions of PowerShellGet, the newly built module cannot be imported into that session.
The new module can only be imported into a new session that has no prior imported PowerShellGet module. You will recieve warning messages in the console if you encounter this issue.

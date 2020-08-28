
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/PowerShell/PowerShellGet/blob/development/LICENSE)
[![Documentation - PowerShellGet](https://img.shields.io/badge/Documentation-PowerShellGet-blue.svg)](https://msdn.microsoft.com/en-us/powershell/gallery/psget)
[![PowerShell Gallery - PowerShellGet](https://img.shields.io/badge/PowerShell%20Gallery-PowerShellGet-blue.svg)](https://www.powershellgallery.com/packages/PowerShellGet)
[![Minimum Supported PowerShell Version](https://img.shields.io/badge/PowerShell-5.0-blue.svg)](https://github.com/PowerShell/PowerShellGet)

Important Note
==============
This version of PowerShellGet is currently under development and is not feature complete.
As a result, we are currently not accepting PRs to this repository. 
Please open an issue for any feature requests, bug reports, or questions for PowerShellGet version 3.0 (currently available as a preview release).
Please note, the repository for previous versions of PowerShellGet has a new location at [PowerShell/PowerShellGetv2](https://github.com/PowerShell/PowerShellGetv2).

Introduction
============

PowerShellGet is a PowerShell module with commands for discovering, installing, updating and publishing the PowerShell artifacts like Modules, DSC Resources, Role Capabilities and Scripts.

PowerShellGet module is also integrated with the PackageManagement module as a provider, users can also use the PackageManagement cmdlets for discovering, installing and updating the PowerShell artifacts like Modules and Scripts.


Documentation
=============

Documentation for PowerShellGet 3.0 has not yet been published, please
[Click here](https://docs.microsoft.com/powershell/module/PowerShellGet/?view=powershell-7)
to reference the documentation for previous versions of PowerShellGet.

Requirements
============

- Windows PowerShell 5.0 or newer.
- PowerShell Core.


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
* Navigate to the source directory
```powershell
cd path/to/PowerShellGet/src
```

* Build the project
```
dotnet publish --framework netstandard2.0
dotnet publish --framework net472
```

* Import the module
```powershell
# If running PowerShell 6+
Import-Module .\bin\Debug\netstandard2.0\publish\PowerShellGet.dll

# if running Windows PowerShell
Import-Module .\bin\Debug\netstandard2.0\publish\PowerShellGet.dll
```


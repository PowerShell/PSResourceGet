
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/PowerShell/PowerShellGet/blob/development/LICENSE)
[![Documentation - PowerShellGet](https://img.shields.io/badge/Documentation-PowerShellGet-blue.svg)](https://msdn.microsoft.com/en-us/powershell/gallery/psget)
[![PowerShell Gallery - PowerShellGet](https://img.shields.io/badge/PowerShell%20Gallery-PowerShellGet-blue.svg)](https://www.powershellgallery.com/packages/PowerShellGet)
[![Minimum Supported PowerShell Version](https://img.shields.io/badge/PowerShell-5.0-blue.svg)](https://github.com/PowerShell/PowerShellGet)

Introduction
============

PowerShellGet is a PowerShell module with commands for discovering, installing, updating and publishing the PowerShell artifacts like Modules, DSC Resources, Role Capabilities and Scripts.

PowerShellGet module is also integrated with the PackageManagement module as a provider, users can also use the PackageManagement cmdlets for discovering, installing and updating the PowerShell artifacts like Modules and Scripts.


Documentation
=============

[Click here](https://msdn.microsoft.com/en-us/powershell/gallery/psget/overview)


Requirements
============

- Windows PowerShell 5.0 or newer.
- PowerShell Core.


Get PowerShellGet Module
========================

Please refer to our [documentation](https://aka.ms/installing-psget) for the up-to-date version on how to get the PowerShellGet Module.


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
cd path/to/PowerShellGet
```

* Import the module
```powershell
Import-Module src/PowerShellGet
```


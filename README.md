# PSResourceGet

> [!NOTE] `PSResourceGet` is short for the full name `Microsoft.PowerShell.PSResourceGet`

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/PowerShell/PSResourceGet/blob/master/LICENSE)
[![Documentation - PSResourceGet](https://img.shields.io/badge/Documentation-PowerShellGet-blue.svg)](https://docs.microsoft.com/en-us/powershell/module/powershellget/?view=powershell-7.1)
[![PowerShell Gallery - PSResourceGet](https://img.shields.io/badge/PowerShell%20Gallery-PSResourceGet-blue.svg)](https://www.powershellgallery.com/packages/Microsoft.PowerShell.PSResourceGet)
[![Minimum Supported PowerShell Version](https://img.shields.io/badge/PowerShell-5.0-blue.svg)](https://github.com/PowerShell/PSResourceGet)

## Important Note

If you were familiar with the PowerShellGet 3.0 project, we renamed the module to be PSResourceGet, for more information please read [this blog](https://devblogs.microsoft.com/powershell/powershellget-in-powershell-7-4-updates/).

If you would like to open a PR please open an issue first so that necessary discussion can take place.
Please open an issue for any feature requests, bug reports, or questions for PSResourceGet.
Please note, the repository for PowerShellGet v2 is available at [PowerShell/PowerShellGetv2](https://github.com/PowerShell/PowerShellGetv2).
The repository for the PowerShellGet v3, the compatibility layer between PowerShellGet v2 and PSResourceGet, is available at [PowerShell/PowerShellGet](https://github.com/PowerShell/PowerShellGet).

## Introduction

PSResourceGet is a PowerShell module with commands for discovering, installing, updating and publishing the PowerShell resources like Modules, Scripts, and DSC Resources.

## Documentation

Documentation for PSResourceGet is currently under its old name PowerShellGet v3, please [Click here](https://learn.microsoft.com/en-ca/powershell/module/microsoft.powershell.psresourceget/?view=powershellget-3.x) to reference the documentation.

## Requirements

* PowerShell 5.0 or higher.

## How To

### Install the PSResourceGet module

* `PSResourceGet` is short for the full name `Microsoft.PowerShell.PSResourceGet`.
* It's included in PowerShell since v7.4.
Please use the [PowerShell Gallery](https://www.powershellgallery.com) to get the latest version of the module.

### Get the source code

* Download the latest source code from the release page (<https://github.com/PowerShell/PSResourceGet/releases>) OR clone the repository (requires git)
  ```powershell
  git clone https://github.com/PowerShell/PSResourceGet
  ```
* Navigate to the local repository directory
  ```powershell
  PS C:\> cd c:\Repos\PSResourceGet
  PS C:\Repos\PSResourceGet>
  ```

### Build the project

```powershell
# Build for the net472 framework
PS C:\Repos\PSResourceGet> .\build.ps1 -Clean -Build -BuildConfiguration Debug -BuildFramework net472

# Build for the netstandard2.0 framework
PS C:\Repos\PSResourceGet> .\build.ps1 -Clean -Build -BuildConfiguration Debug -BuildFramework netstandard2.0
```

### Publish the module to a local repository

```powershell
PS C:\Repos\PSResourceGet> .\build.ps1 -Publish
```

### Run functional tests

Requires the [PSPackageProject](https://www.powershellgallery.com/packages/PSPackageProject) module.

```powershell
PS C:\Repos\PSResourceGet> Invoke-PSPackageProjectTest -Type Functional
```

* Import the module into a new PowerShell session

```powershell
# If running PowerShell 6+
C:\> Import-Module C:\Repos\PSResourceGet\out\PSResourceGet

# If running Windows PowerShell
C:\> Import-Module C:\Repos\PSResourceGet\out\PSResourceGet\PSResourceGet.psd1
```

## Code of Conduct

Please see our [Code of Conduct](CODE_OF_CONDUCT.md) before participating in this project.

## Security Policy

For any security issues, please see our [Security Policy](SECURITY.md).

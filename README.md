# PSResourceGet

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/PowerShell/PSResourceGet/blob/master/LICENSE)
[![Documentation - PSResourceGet](https://img.shields.io/badge/Documentation-PowerShellGet-blue.svg)](https://learn.microsoft.com/powershell/module/microsoft.powershell.psresourceget)
[![PowerShell Gallery - PSResourceGet](https://img.shields.io/badge/PowerShell%20Gallery-PSResourceGet-blue.svg)](https://www.powershellgallery.com/packages/Microsoft.PowerShell.PSResourceGet)
[![Minimum Supported PowerShell Version](https://img.shields.io/badge/PowerShell-5.0-blue.svg)](https://github.com/PowerShell/PSResourceGet)

## Important Notes

> [!NOTE]
> `PSResourceGet` is short for the full name of the module, `Microsoft.PowerShell.PSResourceGet`.  The full name is what is used in PowerShell and when published to the [PowerShell Gallery](https://www.powershellgallery.com/packages/Microsoft.PowerShell.PSResourceGet).

* If you were familiar with the PowerShellGet 3.0 project, we renamed the module to be PSResourceGet, for more information please read [this blog](https://devblogs.microsoft.com/powershell/powershellget-in-powershell-7-4-updates/).
* If you would like to open a PR please open an issue first so that necessary discussion can take place.
  * Please open an issue for any feature requests, bug reports, or questions for PSResourceGet.
  * See the [Contributing Quickstart Guide](#contributing-quickstart-guide) section.
* Please note, the repository for PowerShellGet v2 is available at [PowerShell/PowerShellGetv2](https://github.com/PowerShell/PowerShellGetv2).
* The repository for the PowerShellGet v3, the compatibility layer between PowerShellGet v2 and PSResourceGet, is available at [PowerShell/PowerShellGet](https://github.com/PowerShell/PowerShellGet).

## Introduction

PSResourceGet is a PowerShell module with commands for discovering, installing, updating and publishing the PowerShell resources like Modules, Scripts, and DSC Resources.

## Documentation

[Click here](https://learn.microsoft.com/powershell/module/microsoft.powershell.psresourceget) to reference the documentation.

## Requirements

* PowerShell 5.0 or higher.

## Install the PSResourceGet module

* `PSResourceGet` is short for the full name `Microsoft.PowerShell.PSResourceGet`.
* It's included in PowerShell since v7.4.
Please use the [PowerShell Gallery](https://www.powershellgallery.com) to get the latest version of the module.

## Contributing Quickstart Guide

### Get the source code

* Download the latest source code from the release page (<https://github.com/PowerShell/PSResourceGet/releases>) OR clone the repository using git.
  ```powershell
  PS > cd 'C:\Repos'
  PS C:\Repos> git clone https://github.com/PowerShell/PSResourceGet
  ```
* Navigate to the local repository directory
  ```powershell
  PS C:\> cd c:\Repos\PSResourceGet
  PS C:\Repos\PSResourceGet>
  ```

### Build the project

Note:  Please ensure you have the exact version of the .NET SDK installed. The current version can be found in the [global.json](https://github.com/PowerShell/PSResourceGet/blob/master/global.json) and installed from the [.NET website](https://dotnet.microsoft.com/en-us/download).
  ```powershell
  # Build for the net472 framework
  PS C:\Repos\PSResourceGet> .\build.ps1 -Clean -Build -BuildConfiguration Debug -BuildFramework net472
  ```

### Run functional tests

* Run all tests
  ```powershell
  PS C:\Repos\PSResourceGet> Invoke-Pester
  ```
* Run an individual test
  ```powershell
  PS C:\Repos\PSResourceGet> Invoke-Pester <file-name>
  ```

### Import the built module into a new PowerShell session

```powershell
# If running PowerShell 6+
C:\> pwsh
C:\> Import-Module C:\Repos\PSResourceGet\out\PSResourceGet

# If running Windows PowerShell
c:\> PowerShell
C:\> Import-Module C:\Repos\PSResourceGet\out\PSResourceGet\PSResourceGet.psd1
```
## Module Support Lifecycle 
Microsoft.PowerShell.PSResourceGet follows the support lifecycle of the version of PowerShell that it ships in. 
For example, PSResourceGet 1.0.x shipped in PowerShell 7.4 which is an LTS release so it will be supported for 3 years.
Preview versions of the module, or versions that ship in preview versions of PowerShell are not supported.
Versions of PSResourceGet that do not ship in a version of PowerShell will be fixed forward.

## Code of Conduct

Please see our [Code of Conduct](CODE_OF_CONDUCT.md) before participating in this project.

## Security Policy

For any security issues, please see our [Security Policy](SECURITY.md).

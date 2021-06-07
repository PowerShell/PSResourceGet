# PowerShellGet V3 Module Design

## Description

PowerShellGet V3 is an upgrade to the currently available V2 module.
The V2 module is completely script based, and has dependencies on other PowerShell modules(PackageManagement).  

This version directly uses NuGet APIs, via NuGet managed code binaries.  

For more information, see [Re-architecting PowerShellGet - the PowerShell package manager](https://github.com/PowerShell/PowerShell-RFC/pull/185).  

## Goals

- Works side by side with current PowerShellGet V2 module

- Remove dependency on PackageManagement module, and directly use NuGet APIs

- Leverage the latest NuGet V3 APIs

- Provide cmdlets that perform similar functions but do not interfere with V2 cmdlets

- Implement as binary cmdlets and minimize use of PowerShell scripts

- Remove unneeded components (DscResources).  TODO: Discuss with Sydney and Steve.

- Minimize binary dependencies

- Work over all PowerShell supported platforms

- Minimize code duplication

- Have only one .NET dependency (netstandard2.0) for Windows 5.x compatibility

## Compatibility Module

### Update module as needed

### Write/update tests as needed

## Summary of work estimates

### Cmdlet work estimates

TODO:

### Compatibility Module work estimates

TODO:

## Cmdlets

### Find-PSResource

[Find-PSResource](./FindPSResource.md)

### Get-PSResource

### Get-PSResourceRepository

### Install-PSResource

### Publish-PSResource

### Register-PSResourceRepository

### Save-PSResource

### Set-PSResourceRepository

### Uninstall-PSResource

### Unregister-PSResourceRepository

### Update-PSResource

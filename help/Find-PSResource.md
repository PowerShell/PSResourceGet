---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.date: 08/03/2022
online version:
schema: 2.0.0
---

# Find-PSResource

## SYNOPSIS
Searches for packages from a repository (local or remote), based on a name or other package
properties.

## SYNTAX

### ResourceNameParameterSet (Default)

```
Find-PSResource [[-Name] <string[]>] [-Type <ResourceType>] [-Version <string>] [-Prerelease]
 [-Tag <string[]>] [-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies]
 [<CommonParameters>]
```

### CommandNameParameterSet

```
Find-PSResource -CommandName <string[]> [-Version <string>] [-Prerelease] [-ModuleName <string[]>]
 [-Tag <string[]>] [-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies]
 [<CommonParameters>]
```

### DscResourceNameParameterSet

```
Find-PSResource -DscResourceName <string[]> [-Version <string>] [-Prerelease]
 [-ModuleName <string[]>] [-Tag <string[]>] [-Repository <string[]>] [-Credential <pscredential>]
 [-IncludeDependencies] [<CommonParameters>]
```

## DESCRIPTION

The `Find-PSResource` cmdlet searches for a package from a repository (local or remote) based on a
name or other package properties.

## EXAMPLES

### Example 1

This examples searches PowerShell Gallery for the **PowerShellGet** package. The cmdlet returns the
highest non-prerelease version.

```powershell
Find-PSResource -Name PowerShellGet -Repository PSGallery
```

```Output
Name          Version Prerelease Repository Description
----          ------- ---------- ---------- -----------
PowerShellGet 2.2.5.0            PSGallery  PowerShell module with commands for discovering, installing, updating and …
```

### Example 2

This examples searches PowerShell Gallery for the **PowerShellGet** package, including prerelease
versions.

```powershell
Find-PSResource -Name PowerShellGet -Repository PSGallery -Prerelease
```

```Output
Name          Version  Prerelease Repository Description
----          -------  ---------- ---------- -----------
PowerShellGet 3.0.14.0 beta14     PSGallery  PowerShell module with commands for discovering, installing, updating and…
```

### Example 3

This examples searches PowerShell Gallery for the **Microsoft.PowerShell.SecretManagement** package.
The cmdlet returns all versions that satisfy the specified **Version** range.

```powershell
Find-PSResource -Name "Microsoft.PowerShell.SecretManagement" -Version "(0.9.0.0, 1.2.0.0]" -Repository PSGallery -Prerelease
```

```Output
Name                                  Version Prerelease Repository Description
----                                  ------- ---------- ---------- -----------
Microsoft.PowerShell.SecretManagement 1.1.2.0            PSGallery  …
Microsoft.PowerShell.SecretManagement 1.1.1.0            PSGallery  …
Microsoft.PowerShell.SecretManagement 1.1.0.0            PSGallery  …
Microsoft.PowerShell.SecretManagement 1.0.0.0            PSGallery  …
Microsoft.PowerShell.SecretManagement 0.9.1.0            PSGallery  …
```

### Example 4

This examples searches for all module resources containing the **CommandName** of
`Get-TargetResource`. The cmdlet returns all the module resources that include the command.

```powershell
Find-PSResource -CommandName Get-TargetResource -Repository PSGallery |
    Select-Object -ExpandProperty ParentResource
```

```Output
Name                       Version    Prerelease Repository Description
----                       -------    ---------- ---------- -----------
xPowerShellExecutionPolicy 3.1.0.0               PSGallery  This DSC resource can change the user preference for the W…
WindowsDefender            1.0.0.4               PSGallery  Windows Defender module allows you to configure Windows De…
SystemLocaleDsc            1.2.0.0               PSGallery  This DSC Resource allows configuration of the Windows Syst…
xInternetExplorerHomePage  1.0.0.0               PSGallery  This DSC Resources can easily set an URL for the home page…
OctopusDSC                 4.0.1127.0            PSGallery  Module with DSC resource to install and configure an Octop…
cRegFile                   1.2.0.0               PSGallery  DSC resource which is designed to manage large numbers of …
cWindowsErrorReporting     1.1.0.0               PSGallery  DSC Resource to enable or disable Windows Error Reporting
cVNIC                      1.0.0.0               PSGallery  DSC Module to create and configuring virutal network adapt…
supVsts                    1.1.17.0              PSGallery  Dsc module for interfacing with VSTS.
```

### Example 5

This examples searches for a module resource with a a specific module with a named command.

```powershell
Find-PSResource -CommandName Get-TargetResource -ModuleName SystemLocaleDsc -Repository PSGallery |
    Select-Object -ExpandProperty ParentResource
```

```Output
Name            Version Prerelease Repository Description
----            ------- ---------- ---------- -----------
SystemLocaleDsc 1.2.0.0            PSGallery  This DSC Resource allows configuration of the Windows System Locale.
```

### Example 6

This examples searches for all module resources containing the DSC Resource `SystemLocale`.

```powershell
Find-PSResource -DscResourceName SystemLocale -Repository PSGallery |
    Select-Object -ExpandProperty ParentResource
```

```Output
Name                  Version Prerelease Repository Description
----                  ------- ---------- ---------- -----------
ComputerManagementDsc 8.5.0.0            PSGallery  DSC resources for configuration of a Windows computer. These DSC r…
SystemLocaleDsc       1.2.0.0            PSGallery  This DSC Resource allows configuration of the Windows System Local…
```

### Example 7

This example searches all registered PSResourceRepositories for resources with names starting with
`Computer`.

```powershell
Find-PSResource -Name Computer*
```

```Output
Name                                              Version Prerelease Repository       Description
----                                              ------- ---------- ----------       -----------
ComputerManagementDsc                             8.5.0.0            PSGallery        DSC resources for configuration …
ComputerManagement                                1.1.2.3            PSGallery        A PowerShell module for working …
Computer_JoinDomain_Config                        1.0.0.0            PSGalleryScripts This configuration sets the mach…
Computer_UnjoinDomainAndJoinWorkgroup_Config      1.0.0.0            PSGalleryScripts This example switches the comput…
Computer_SetComputerDescriptionInWorkgroup_Config 1.0.0.0            PSGalleryScripts This example will set the comput…
Computer_JoinDomainSpecifyingDC_Config            1.0.0.0            PSGalleryScripts This configuration sets the mach…
Computer_RenameComputerAndSetWorkgroup_Config     1.0.0.0            PSGalleryScripts This configuration will set the …
Computer_RenameComputerInDomain_Config            1.0.0.0            PSGalleryScripts This example will change the mac…
Computer_RenameComputerInWorkgroup_Config         1.0.0.0            PSGalleryScripts This example will set the machin…
```

### Example 8

This example shows how to find modules by a tag. The `CrescendoBuilt` value is a tag that is
automatically added to modules created using the **Microsoft.PowerShell.Crescendo** module.

```powershell
Find-PSResource -Tag CrescendoBuilt
```

```Output
Name            Version Prerelease Repository Description
----            ------- ---------- ---------- -----------
Foil            0.1.0.0            PSGallery  A PowerShell Crescendo wrapper for Chocolatey
Cobalt          0.3.1.0            PSGallery  A PowerShell Crescendo wrapper for WinGet
SysInternals    1.1.0.0            PSGallery  PowerShell cmdlets for SysInternal tools
Croze           0.0.4.0            PSGallery  A PowerShell Crescendo wrapper for Homebrew
AptPackage      0.0.2.0            PSGallery  PowerShell Crescendo-generated Module to query APT-Package Information
RoboCopy        1.0.1.0            PSGallery  PowerShell cmdlet for the official RoboCopy.exe
TShark          1.0.2.0            PSGallery  PowerShell cmdlet for tshark.exe
Image2Text      1.0.2.0            PSGallery  PowerShell Images into ASCII art
SpeedTestCLI    1.0.0.0            PSGallery  PowerShell cmdlets speedtest-cli
SpeedTest-CLI   1.0.0.0            PSGallery  PowerShell cmdlets for Internet Speed Test
Quser.Crescendo 0.1.1.0            PSGallery  This module displays session information of users logged onto a local or…
Takeown         1.0.2.0            PSGallery  Crescendo Powershell wrapper of takeown.exe
```

## PARAMETERS

### -CommandName

The name of the command to search for.

```yaml
Type: System.String[]
Parameter Sets: CommandNameParameterSet
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential

Optional credentials to be used when accessing a repository.

```yaml
Type: System.Management.Automation.PSCredential
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DscResourceName

The name of the DSC Resource to search for.

```yaml
Type: System.String[]
Parameter Sets: DscResourceNameParameterSet
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeDependencies

When specified, search returns all matching resources their dependencies. Dependencies are
deduplicated.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -ModuleName

Specifies a module resource package name type to search for. Wildcards are supported.

Not yet implemented.

```yaml
Type: System.String[]
Parameter Sets: CommandNameParameterSet, DscResourceNameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name

Name of a resource to find. Wildcards are supported but NuGet only accepts the `*` character. NuGet
does not support wildcard searches of local (file-based) repositories.


```yaml
Type: System.String[]
Parameter Sets: ResourceNameParameterSet
Aliases:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -Prerelease

When specified, includes prerelease versions in search results returned.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository

Specifies one or more repository names to search. Wildcards are supported.

If not specified, search includes all registered repositories, in priority order (highest first),
until a repository is found that contains the package.

Lower **Priority** values have a higher precedence.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Tag

Filters search results for resources that include one or more of the specified tags.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type

Specifies one or more resource types to find. Resource types supported are:

- `Module`
- `Script`
- `Command`
- `DscResource`

```yaml
Type: Microsoft.PowerShell.PowerShellGet.UtilClasses.ResourceType
Parameter Sets: ResourceNameParameterSet
Aliases:
Accepted values: Module, Script, DscResource, Command

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Version

Specifies the version of the resource to be returned. The value can be an exact version or a version
range using the NuGet versioning syntax.

Wildcards are supported but NuGet only accepts wildcard character `*`. For more information about
NuGet version ranges, see [Package versioning](/nuget/concepts/package-versioning#version-ranges).

PowerShellGet supports all but the _minimum inclusive version_ listed in the NuGet version range
documentation. Using `1.0.0.0` as the version doesn't yield versions 1.0.0.0 and higher (minimum
inclusive range). Instead, the value is considered to be the required version. To search for a
minimum inclusive range, use `[1.0.0.0, ]` as the version range.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose,
-WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo

## NOTES

## RELATED LINKS

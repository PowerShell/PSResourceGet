---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.custom: v3-beta20
ms.date: 04/03/2023
schema: 2.0.0
---

# Find-PSResource

## SYNOPSIS

Searches for packages from a repository (local or remote), based on a name or other package
properties.

## SYNTAX

### NameParameterSet (Default)

```
Find-PSResource [[-Name] <String[]>] [-Type <ResourceType>] [-Version <String>] [-Prerelease] [-Tag <String[]>]
 [-Repository <String[]>] [-Credential <PSCredential>] [-IncludeDependencies] [<CommonParameters>]
```

### CommandNameParameterSet

```
Find-PSResource [-Prerelease] -CommandName <String[]> [-Repository <String[]>] [-Credential <PSCredential>]
 [<CommonParameters>]
```

### DscResourceNameParameterSet

```
Find-PSResource [-Prerelease] -DscResourceName <String[]> [-Repository <String[]>] [-Credential <PSCredential>]
 [<CommonParameters>]
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
PowerShellGet 2.2.5              PSGallery  PowerShell module with commands for discovering, installing, updating and p…


```

### Example 2

This examples searches PowerShell Gallery for the **PowerShellGet** package, including prerelease
versions.

```powershell
Find-PSResource -Name PowerShellGet -Repository PSGallery -Prerelease
```

```Output
Name          Version Prerelease Repository Description
----          ------- ---------- ---------- -----------
PowerShellGet 3.0.20  beta20     PSGallery  PowerShell module with commands for discovering, installing, updating and p…
```

### Example 3

This examples searches PowerShell Gallery for the **Microsoft.PowerShell.SecretManagement** package.
The cmdlet returns all versions that satisfy the specified **Version** range.

```powershell
$parameters = @{
    Name = 'Microsoft.PowerShell.SecretManagement'
    Version = '(0.9.0.0, 1.2.0.0]'
    Repository = 'PSGallery'
    Prerelease = $true
}
Find-PSResource @parameters
```

```Output
Name                                  Version Prerelease Repository Description
----                                  ------- ---------- ---------- -----------
Microsoft.PowerShell.SecretManagement 1.1.2              PSGallery  …
Microsoft.PowerShell.SecretManagement 1.1.1              PSGallery  …
Microsoft.PowerShell.SecretManagement 1.1.0   preview2   PSGallery  …
Microsoft.PowerShell.SecretManagement 1.1.0   preview    PSGallery  …
Microsoft.PowerShell.SecretManagement 1.1.0              PSGallery  …
Microsoft.PowerShell.SecretManagement 1.0.1              PSGallery  …
Microsoft.PowerShell.SecretManagement 1.0.0              PSGallery  …
Microsoft.PowerShell.SecretManagement 0.9.1              PSGallery  …
```

### Example 4

This examples searches for all module resources containing the **CommandName** of
`Get-TargetResource`. The cmdlet returns all the module resources that include the command.

```powershell
Find-PSResource -CommandName Get-TargetResource -Repository PSGallery
```

```Output
Name                 Package Name               Version
----                 ------------               -------
{Get-TargetResource} cRegFile                   1.2
{Get-TargetResource} cVNIC                      1.0.0.0
{Get-TargetResource} cWindowsErrorReporting     1.1
{Get-TargetResource} OctopusDSC                 4.0.1131
{Get-TargetResource} supVsts                    1.1.17.0
{Get-TargetResource} SystemLocaleDsc            1.2.0.0
{Get-TargetResource} WindowsDefender            1.0.0.4
{Get-TargetResource} xInternetExplorerHomePage  1.0.0
{Get-TargetResource} xPowerShellExecutionPolicy 3.1.0.0
```

### Example 5

This examples searches for all module resources containing the DSC Resource `SystemLocale`.

```powershell
Find-PSResource -DscResourceName SystemLocale -Repository PSGallery
```

```Output
Name           Package Name          Version
----           ------------          -------
{SystemLocale} ComputerManagementDsc 9.0.0
{SystemLocale} SystemLocaleDsc       1.2.0.0
```

### Example 6

This example searches all registered PSResourceRepositories for resources with names starting with
`Computer`.

```powershell
Find-PSResource -Name Computer*
```

```Output
Name                                              Version Prerelease Repository Description
----                                              ------- ---------- ---------- -----------
ComputerManagementDsc                             9.0.0              PSGallery  DSC resources for configuration of a Wi…
ComputerManagement                                1.1.2.3            PSGallery  A PowerShell module for working with th…
ComputerCleanup                                   1.2.0              PSGallery  Module for freeing up disk space / remo…
Computer_UnjoinDomainAndJoinWorkgroup_Config      1.0.0              PSGallery  This example switches the computer 'Ser…
Computer_SetComputerDescriptionInWorkgroup_Config 1.0.0              PSGallery  This example will set the computer desc…
Computer_RenameComputerInWorkgroup_Config         1.0.0              PSGallery  This example will set the machine name …
Computer_RenameComputerInDomain_Config            1.0.0              PSGallery  This example will change the machines n…
Computer_RenameComputerAndSetWorkgroup_Config     1.0.0              PSGallery  This configuration will set the compute…
Computer_JoinDomainSpecifyingDC_Config            1.0.0              PSGallery  This configuration sets the machine nam…
Computer_JoinDomain_Config                        1.0.0              PSGallery  This configuration sets the machine nam…
```

### Example 7

This example shows how to find modules by a tag. The `CrescendoBuilt` value is a tag that's
automatically added to modules created using the **Microsoft.PowerShell.Crescendo** module.

```powershell
Find-PSResource -Tag CrescendoBuilt
```

```Output
Name            Version Prerelease Repository Description
----            ------- ---------- ---------- -----------
AptPackage      0.0.2              PSGallery  PowerShell Crescendo-generated Module to query APT-Package Information
Cobalt          0.4.0              PSGallery  A PowerShell Crescendo wrapper for WinGet
Croze           0.0.5              PSGallery  A PowerShell Crescendo wrapper for Homebrew
Foil            0.3.0              PSGallery  A PowerShell Crescendo wrapper for Chocolatey
Image2Text      1.0.2              PSGallery  PowerShell Images into ASCII art
pastel          1.0.1              PSGallery  PowerShell commands for pastel
PSDupes         0.0.1              PSGallery  A crescendo module to locate duplicate files. Very fast and easy to use, …
psFilesCli      0.0.3              PSGallery  A PowerShell wrapper for files-cli.exe
PSLogParser     0.0.2              PSGallery  Crescendo Powershell module for Log Parser 2.2
Quser.Crescendo 0.1.3              PSGallery  This module displays session information of users logged onto a local or …
RoboCopy        1.0.1              PSGallery  PowerShell cmdlet for the official RoboCopy.exe
SpeedTest-CLI   1.0.1              PSGallery  PowerShell cmdlets for Internet Speed Test
SpeedTestCLI    1.0.0              PSGallery  PowerShell cmdlets speedtest-cli
SysInternals    1.1.0              PSGallery  PowerShell cmdlets for SysInternal tools
Takeown         1.0.2              PSGallery  Crescendo Powershell wrapper of takeown.exe
TShark          1.0.2              PSGallery  PowerShell cmdlet for tshark.exe
VssAdmin        0.8.0              PSGallery  This is a Crescendo module to wrap the Windows `vssadmin.exe` command-lin…
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
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name

Name of a resource to find. Wildcards are supported but NuGet only accepts the `*` character. NuGet
doesn't support wildcard searches of local (file-based) repositories.

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet
Aliases:

Required: True
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
Parameter Sets: NameParameterSet
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
Parameter Sets: NameParameterSet
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
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCommandResourceInfo

## NOTES

## RELATED LINKS

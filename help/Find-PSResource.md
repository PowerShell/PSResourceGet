---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# Find-PSResource

## SYNOPSIS
Searches for packages from a repository (local or remote), based on `-Name` and other package properties.

## SYNTAX

### ResourceNameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-Type <Microsoft.PowerShell.PowerShellGet.UtilClasses.ResourceType[]>] [-Version <string>] [-Prerelease] [-Tag <string[]>] [-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### CommandNameParameterSet
``` PowerShell
[[-CommandName] <string[]>] [-ModuleName <string[]>] [-Version <string>] [-Prerelease] [-Tag <string[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### DscResourceNameParameterSet
``` PowerShell
[[-DscResourceName] <string[]>] [-ModuleName <string[]>] [-Version <string>] [-Prerelease] [-Tag <string[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### TagParameterSet
``` PowerShell
[[-Name <string>][-Tag <string[]>] [-Prerelease]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### TypeParameterSet
``` PowerShell
[[Name <string>] [-Prerelease]  [-Type <Microsoft.PowerShell.PowerShellGet.UtilClasses.ResourceType[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
The `Find-PSResource` cmdlet searches for a package from a repository (local or remote) based on `-Name` or other package properties.

## EXAMPLES
These examples assume that the PSGallery repository is registered and contains the packages we are searching for.
### Example 1
```powershell
PS C:\> Find-PSResource -Name "Microsoft.PowerShell.SecretManagement" -Repository PSGallery
        Name                                    Version                         Prerelease   Description
        ----                                    -------                         ----------   -----------
        Microsoft.PowerShell.SecretManagement   1.0.0.0                                      This module ...
```

This examples searches for the package with `-Name` "Microsoft.PowerShell.SecretManagement". It returns the highest non-prerelease version for the package found by searching through the `-Repository` "PSGallery", which at the time of writing this example is version "1.0.0.0".

### Example 2
```powershell
PS C:\> Find-PSResource -Name "Microsoft.PowerShell.SecretManagement" -Repository PSGallery -Prerelease
        Name                                    Version                         Prerelease   Description
        ----                                    -------                         ----------   -----------
        Microsoft.PowerShell.SecretManagement   1.1.0.0                         preview2     This module ...
```

This examples searches for the package with `-Name` "Microsoft.PowerShell.SecretManagement". It returns the highest version (including considering prerelease versions) for the package found by searching through the specified `-Repository` "PSGallery", which at the time of writing this example is version "1.1.0-preview2".

### Example 3
```powershell
PS C:\> Find-PSResource -Name "Microsoft.PowerShell.SecretManagement" -Version "(0.9.0.0, 1.0.0.0]" -Repository PSGallery -Prerelease
        Name                                    Version                         Prerelease   Description
        ----                                    -------                         ----------   -----------
        Microsoft.PowerShell.SecretManagement   0.9.1.0                                      This module ...
        Microsoft.PowerShell.SecretManagement   1.0.0.0                                      This module ...
```

This examples searches for the package with `-Name` "Microsoft.PowerShell.SecretManagement". It returns all versions which satisfy the specified `-Version` range by looking through the specified `-Repository` "PSGallery". At the time of writing this example those satisfying versions are: "0.9.1.0" and "1.0.0.0".

### Example 4
```powershell
PS C:\> Find-PSResource -CommandName "Get-TargetResource" -Repository PSGallery
        Name                                    Version    Prerelease   ModuleName                     Repository
        ----                                    -------    ----------   ----------                     ----------
        Get-TargetResource                      3.1.0.0                 xPowerShellExecutionPolicy     PSGallery
        Get-TargetResource                      1.0.0.4                 WindowsDefender                PSGallery
        Get-TargetResource                      1.2.0.0                 SystemLocaleDsc                PSGallery
        Get-TargetResource                      1.0.0.0                 xInternetExplorerHomePage      PSGallery
        Get-TargetResource                      4.0.1055.0              OctopusDSC                     PSGallery
        Get-TargetResource                      1.2.0.0                 cRegFile                       PSGallery
        Get-TargetResource                      1.1.0.0                 cWindowsErrorReporting         PSGallery
        Get-TargetResource                      1.0.0.0                 cVNIC                          PSGallery
        Get-TargetResource                      1.1.17.0                supVsts                        PSGallery

```

This examples searches for all module resources with `-CommandName` "Get-TargetResource" from the `-Repository` PSGallery. It returns all the module resources which include a command named "Get-TargetResource" and also lists the following information for each module resource: version, name (displayed under ModuleName) and repository. To access the rest of the properties of the parent module resource, you can access the `$_.ParentResource` of the PSIncludedResourceInfo object returned from the CommandName parameter set.

### Example 5
```powershell
PS C:\> Find-PSResource -CommandName "Get-TargetResource" -ModuleName "SystemLocaleDsc" -Repository PSGallery
        Name                                    Version    Prerelease   ModuleName                     Repository
        ----                                    -------    ----------   ----------                     ----------
        Get-TargetResource                      1.2.0.0                 SystemLocaleDsc                PSGallery
```

This examples searches for a module resource with a command named "Get-TargetResource" (via the `-CommandName` parameter), specifically from the module resource "SystemLocaleDsc" (via the `-ModuleName` parameter) from the `-Repository` PSGallery. The "SystemLocaleDsc" resource does indeed include a command named Get-TargetResource so this resource will be returned. The returned object lists the name of the command (displayed under Name) and the following information for the parent module resource: version, name (displayed under ModuleName) and repository. To access the rest of the properties of the parent module resource, you can access the `$_.ParentResource` of the PSIncludedResourceInfo object returned from the CommandName parameter set.

### Example 6
```powershell
PS C:\> Find-PSResource -DscResourceName "SystemLocale" -Repository PSGallery
        Name                                    Version    Prerelease   ModuleName                     Repository
        ----                                    -------    ----------   ----------                     ----------
        Get-TargetResource                      8.5.0.0                 ComputerManagementDsc          PSGallery
        Get-TargetResource                      1.2.0.0                 SystemLocaleDsc                PSGallery

```

This examples searches for all module resources with `-DscResourceName` "SystemLocale" from the `-Repository` PSGallery. It returns all the module resources which include a DSC resource named "SystemLocale" and also lists the following information for each module resource: version, name (displayed under ModuleName) and repository. To access the rest of the properties of the parent module resource, you can access the `$_.ParentResource` of the PSIncludedResourceInfo object returned from the DSCResourceName parameter set.

## PARAMETERS

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

### -IncludeDependencies
When specified, search will return all matched resources along with any resources the matched resources depends on.
Dependencies are deduplicated.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ModuleName
Specifies a module resource package name type to search for.
Wildcards are supported.
Not yet implemented.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Name of a resource or resources to find.
Accepts wild card character '*'.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
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
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository
Specifies one or more repository names to search.
If not specified, search will include all currently registered repositories, in order of highest priority, until a repository is found that contains the package.

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
Specifies one or more resource types to find.
Resource types supported are: Module, Script, Command, DscResource.

```yaml
Type: Microsoft.PowerShell.PowerShellGet.UtilClasses.ResourceType[]
Parameter Sets: (All)
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

For more information about NuGet version ranges, see [Package versioning](/nuget/concepts/package-versioning#version-ranges)

PowerShellGet supports all but the _minimum inclusive version_ listed in the NuGet version range
documentation. So inputting "1.0.0.0" as the version doesn't yield versions 1.0.0.0 and higher
(minimum inclusive range). Instead, the values is considered as the required version and yields
version 1.0.0.0 only (required version). To use the minimum inclusive range, provide `[1.0.0.0, ]` as
the version range.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo

## NOTES

## RELATED LINKS

[<add>](<add>)

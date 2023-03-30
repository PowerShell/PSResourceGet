---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.date: 02/01/2023
schema: 2.0.0
---

# Uninstall-PSResource

## SYNOPSIS

Uninstalls a resource that was installed using **PowerShellGet**.

## SYNTAX

### NameParameterSet (Default)

```
Uninstall-PSResource [-Name] <String[]> [-Version <String>] [-Prerelease] [-SkipDependencyCheck]
 [-Scope <ScopeType>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObjectParameterSet

```
Uninstall-PSResource [-Prerelease] -InputObject <PSResourceInfo> [-SkipDependencyCheck]
 [-Scope <ScopeType>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION

This cmdlet combines the functionality of the `Uninstall-Module` and `Uninstall-Script` cmdlets from
**PowerShellGet** v2. The cmdlet searches the package installation paths for resources that have the
**PowerShellGet** XML metadata file. Matching resources are uninstalled from the system.

By default, the cmdlet checks to see whether the resource being removed is a dependency for another
resource.

## EXAMPLES

### Example 1

Uninstall the latest version of the **Az** module.

```powershell
Uninstall-PSResource Az
```

### Example 2

Uninstall a specific version of the **Az** module.

```powershell
Uninstall-PSResource -name Az -version "5.0.0"
```

### Example 3

Uninstalls all versions of the **Az** module within the specified version range.

```powershell
Uninstall-PSResource -name Az -version "(5.0.0, 7.5.0)"
```

### Example 4

This example assumes that the following versions of **Az** module are already installed:

- 4.0.1-preview
- 4.1.0
- 4.0.2-preview

The `Uninstall-PSResource` cmdlet removes stable and prerelease version that fall within the version
range specified. Per NuGetVersion rules, a prerelease version is less than a stable version, so
4.0.1-preview is actually less than the 4.0.1 version in the specified range. Therefore,
4.0.1-preview isn't removed. Versions 4.1.0 and 4.0.2-preview are removed because they fall within
the range.

```powershell
Uninstall-PSResource -name Az -version "[4.0.1, 4.1.0]"
```

### Example 5

This example assumes that the following versions of **Az** module are already installed:

- 4.0.1-preview
- 4.1.0
- 4.0.2-preview

This is the same as the previous example except the **Prerelease** parameter means that only
prerelease versions are removed. Only version 4.0.2-preview is removed because version 4.0.1-preview
is outside the range and version 4.1.0 isn't a prerelease version.

```powershell
Uninstall-PSResource -name Az -version "[4.0.1, 4.1.0]" -Prerelease
```

## PARAMETERS

### -InputObject

Used for pipeline input.

```yaml
Type: Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo
Parameter Sets: InputObjectParameterSet
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Name

Name of a resource or resources to remove. Wildcards are supported but NuGet only accepts the `*`
character.

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

Indicates that only prerelease version resources should be removed.

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

### -Scope

Specifies the scope of the resource to uninstall.

```yaml
Type: Microsoft.PowerShell.PowerShellGet.UtilClasses.ScopeType
Parameter Sets: (All)
Aliases:
Accepted values: CurrentUser, AllUsers

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipDependencyCheck

By default, the cmdlet checks to see whether the resource being removed is a dependency for another
resource. Using this parameter skips the dependency test.

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

### -Version

Specifies the version of the resource to be removed. The value can be an exact version or a version
range using the NuGet versioning syntax.

For more information about NuGet version ranges, see
[Package versioning](/nuget/concepts/package-versioning#version-ranges).

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
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf

Shows what would happen if the cmdlet runs. The cmdlet isn't run.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

## RELATED LINKS

[Package versioning](/nuget/concepts/package-versioning#version-ranges)

[Install-PSResource](Install-PSResource.md)

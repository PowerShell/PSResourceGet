---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Save-PSResource

## SYNOPSIS
Saves resources (modules and scripts) from a registered repository onto the machine.

## SYNTAX

```
Save-PSResource [-Name] <String[]> [-Version <String>] [-Prerelease] [-Repository <String[]>]
 [-Credential <PSCredential>] [-AsNupkg] [-IncludeXML] [-Path <String>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## DESCRIPTION
The Save-PSResource cmdlet combines the Save-Module and Save-Script cmdlets from V2. 
It saves a resource from a registered repository to a specific path on a machine based on the -Name parameter argument. It does not return any object. Other parameters allow the resource to be specified by repository and version, and allow the user to save the resource as a .nupkg or with the PowerShellGet XML metadata.

## EXAMPLES

### Example 1
```powershell
PS C:\> Save-PSResource -Name Az
```
Saves the Az module 

### Example 2
```powershell
PS C:\> Save-PSResource -Name Az -Repository PSGallery
```
Saves the Az module found in the PowerShellGallery

### Example 3
```powershell
PS C:\> Save-PSResource Az -AsNupkg
```
Saves the Az module as a .nupkg file

### Example 4
```powershell
PS C:\> Save-PSResource Az -IncludeXML
```
Saves the Az module and includes the PowerShellGet XML metadata

## PARAMETERS

### -Name
Name of a resource or resources to save. Does not accept wildcard characters or a null value.

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Version
Specifies the version of the resource to be saved. The value can be an exact version or a version
range using the NuGet versioning syntax.

For more information about NuGet version ranges, see [Package versioning](/nuget/concepts/package-versioning#version-ranges)

PowerShellGet supports all but the _minimum inclusive version_ listed in the NuGet version range
documentation. So inputting "1.0.0.0" as the version doesn't yield versions 1.0.0.0 and higher
(minimum inclusive range). Instead, the values is considered as the required version and yields 
version 1.0.0.0 only (required version). To use the minimum inclusive range, provide `[1.0.0.0, ]` as 
the version range.

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

### -Prerelease
Specifies to include prerelease versions.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository
Specifies one or more repository names to search.
If not specified, search will include all currently registered repositories, in order of highest priority, until first repository package is found in.

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

### -AsNupkg
Saves the resource as a zipped .nupkg file.
```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeXML
Includes the PowerShellGet metadata XML (used to verify that PowerShellGet has installed a module).

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Specifies the path to save the resource to.

```yaml
Type: System.String
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet
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
Parameter Sets: NameParameterSet
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

## OUTPUTS
None

## NOTES

## RELATED LINKS

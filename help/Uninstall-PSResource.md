---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# Uninstall-PSResource

## SYNOPSIS
Uninstalls a resource (module or script) that has been installed on the machine via PowerShellGet.

## SYNTAX

```
Uninstall-PSResource [-Name] <String[]> [-Version <String>] [-Force] [-WhatIf] [<CommonParameters>]
```

## DESCRIPTION
The Uninstall-PSResource cmdlet combines the Uninstall-Module, Uninstall-Script cmdlets from V2. It uninstalls a package found in a module or script installation path based on the -Name parameter argument. It does not return an object. Other parameters allow the returned results to be further filtered.

## EXAMPLES

### Example 1
```powershell
PS C:\> Uninstall-PSResource Az
```

Uninstalls the latest version of the Az module.

### Example 2
```powershell
PS C:\> Uninstall-PSResource -name Az -version "1.0.0"
```

Uninstalls version 1.0.0 of the Az module.

### Example 3
```powershell
PS C:\> Uninstall-PSResource -name Az -version "(1.0.0, 3.0.0)"

Uninstalls all versions within the specified version range.
```

Uninstalls version 1.0.0 of the Az module.

## PARAMETERS

### -Name
Name of a resource or resources that has been installed. Accepts wild card characters.

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

### -Version
Specifies the version of the resource to be uninstalled.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True
Accept wildcard characters: False
```

### -Force
Skips check to see if any modules have a dependency on the resource to be uninstalled.

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

## OUTPUTS
None

## NOTES

## RELATED LINKS

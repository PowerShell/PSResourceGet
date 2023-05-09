---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.custom: v3-beta20
ms.date: 04/03/2023
schema: 2.0.0
---

# Get-PSScriptFileInfo

## SYNOPSIS

Returns the metadata for a script.

## SYNTAX

```
Get-PSScriptFileInfo [-Path] <String> [<CommonParameters>]
```

## DESCRIPTION

This cmdlet searches for a PowerShell script located on the machine and returns the script metadata
information.

## EXAMPLES

### Example 1

This example returns the metadata for the script `MyScript.ps1`.

```powershell
Get-PSScriptFileInfo -Path '.\Scripts\MyScript.ps1'
```

```Output
Name      Version Author              Description
----      ------- ------              -----------
MyScript  1.0.0.0 dev@microsoft.com   This script is a test script for PowerShellGet…
```

## PARAMETERS

### -Path

Specifies the path to the resource.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSScriptFileInfo

## NOTES

## RELATED LINKS

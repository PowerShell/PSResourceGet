---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.date: 02/01/2023
schema: 2.0.0
---

# Get-PSResource

## SYNOPSIS
Returns modules and scripts installed on the machine via **PowerShellGet**.

## SYNTAX

```
Get-PSResource [[-Name] <String[]>] [-Version <String>] [-Path <String>] [-Scope <ScopeType>]
 [<CommonParameters>]
```

## DESCRIPTION

This cmdlet searches the module and script installation paths and returns **PSResourceInfo** objects
that describes each resource item found. This is equivalent to the combined output of the
`Get-InstalledModule` and `Get-InstalledScript` cmdlets from **PowerShellGet** v2.

## EXAMPLES

### Example 1

This example return all versions of modules and scripts installed on the machine.

```powershell
Get-PSResource
```

### Example 2

This example returns all versions of the **Az** module installed using **PowerShellGet**.

```powershell
Get-PSResource Az
```

### Example 3

This example return all versions of the **Az** module installed in the current directory.

```powershell
Get-PSResource Az -Path .
```

### Example 4

This example returns a specific version of the Az module if it's installed on the system.

```powershell
Get-PSResource Az -Version 1.0.0
```

### Example 5

This example return all installed versions of the Az module within the specified version range.

```powershell
Get-PSResource Az -Version "(1.0.0, 3.0.0)"
```

### Example 6

This example returns a specific preview version of the **PowerShellGet** module if it's installed
on the system.

```powershell
Get-PSResource PowerShellGet -Version 3.0.14-beta14
```

```Output
Name          Version Prerelease Repository Description
----          ------- ---------- ---------- -----------
PowerShellGet 3.0.14  beta14     PSGallery  PowerShell module with commands for discovering, installing, updating and …
```

### Example 6

The previous example showed that **PowerShellGet** version 3.0.14-beta14 was installed on the
system. This example shows that you must provide the full version, including the **Prerelease**
label to identify the installed module by **Version**.

```powershell
Get-PSResource PowerShellGet -Version 3.0.14
```

There is no output from this command.

### Example 7

In this example you see that there are four version of **PSReadLine** installed on the system. The
second command searches for a range of version between `2.2.0` and `2.3.0`.

```powershell
Get-PSResource PSReadLine
```

```Output
Name       Version Prerelease Repository Description
----       ------- ---------- ---------- -----------
PSReadLine 2.2.6              PSGallery  Great command line editing in the PowerShell console host
PSReadLine 2.2.5              PSGallery  Great command line editing in the PowerShell console host
PSReadLine 2.2.2              PSGallery  Great command line editing in the PowerShell console host
PSReadLine 2.2.0   beta4      PSGallery  Great command line editing in the PowerShell console host
```

```powershell
Get-PSResource PSReadLine -Version '[2.2.0, 2.3.0]'
```

```Output
Name       Version Prerelease Repository Description
----       ------- ---------- ---------- -----------
PSReadLine 2.2.6              PSGallery  Great command line editing in the PowerShell console host
PSReadLine 2.2.5              PSGallery  Great command line editing in the PowerShell console host
PSReadLine 2.2.2              PSGallery  Great command line editing in the PowerShell console host
```

According to NuGet version rules a prerelease version is less than a stable version, so
`2.2.0-beta4` is less than the `2.2.0` version in the specified version range.

## PARAMETERS

### -Name

Name of a resource to find. Wildcards are supported but NuGet only accepts the `*` character. NuGet
doesn't support wildcard searches of local (file-based) repositories.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -Path

Specifies the path to search in.

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

### -Scope

Specifies the scope of the resource.

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

### -Version

Specifies the version of the resource to be returned. The value can be an exact version or a version
range using the NuGet versioning syntax.

For more information about NuGet version ranges, see
[Package versioning](/nuget/concepts/package-versioning#version-ranges).

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
[about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

## RELATED LINKS

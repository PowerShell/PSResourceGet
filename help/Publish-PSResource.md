---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Publish-PSResource

## SYNOPSIS
{{ Fill in the Synopsis }}

## SYNTAX

### PathParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>] [-Path] <String>
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-ReleaseNotes <String>] [-Tags <String[]>]
 [-LicenseUrl <String>] [-IconUrl <String>] [-ProjectUrl <String>] [-Exclude <String[]>] [-Nuspec <String>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### PathLiteralParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>] -LiteralPath <String>
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-ReleaseNotes <String>] [-Tags <String[]>]
 [-LicenseUrl <String>] [-IconUrl <String>] [-ProjectUrl <String>] [-Exclude <String[]>] [-Nuspec <String>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### CreateNuspecParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>]
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-ReleaseNotes <String>] [-Tags <String[]>]
 [-LicenseUrl <String>] [-IconUrl <String>] [-ProjectUrl <String>] [-Exclude <String[]>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ModuleNameParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>]
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-Exclude <String[]>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### NuspecParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>]
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-Exclude <String[]>] [-Nuspec <String>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
{{ Fill in the Description }}

## EXAMPLES

### Example 1
```powershell
PS C:\> {{ Add example code here }}
```

{{ Add example description here }}

## PARAMETERS

### -APIKey
{{ Fill APIKey Description }}

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

### -Credential
{{ Fill Credential Description }}

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

### -DestinationPath
{{ Fill DestinationPath Description }}

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

### -Exclude
{{ Fill Exclude Description }}

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

### -IconUrl
{{ Fill IconUrl Description }}

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LicenseUrl
{{ Fill LicenseUrl Description }}

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LiteralPath
{{ Fill LiteralPath Description }}

```yaml
Type: System.String
Parameter Sets: PathLiteralParameterSet
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Nuspec
{{ Fill Nuspec Description }}

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, NuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
{{ Fill Path Description }}

```yaml
Type: System.String
Parameter Sets: PathParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -ProjectUrl
{{ Fill ProjectUrl Description }}

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReleaseNotes
{{ Fill ReleaseNotes Description }}

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository
{{ Fill Repository Description }}

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

### -SkipDependenciesCheck
{{ Fill SkipDependenciesCheck Description }}

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

### -Tags
{{ Fill Tags Description }}

```yaml
Type: System.String[]
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
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

### System.String

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS

[<add>](<add>)


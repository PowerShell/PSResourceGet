---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Install-PSResource

## SYNOPSIS
{{ Fill in the Synopsis }}

## SYNTAX

### NameParameterSet (Default)
```
Install-PSResource [-Name] <String[]> [-Type <String[]>] [-Version <String>] [-Prerelease]
 [-Repository <String[]>] [-Credential <PSCredential>] [-Scope <String>] [-NoClobber] [-TrustRepository]
 [-Reinstall] [-Quiet] [-AcceptLicense] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObjectSet
```
Install-PSResource [-InputObject] <Object[]> [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RequiredResourceFileParameterSet
```
Install-PSResource [-Type <String[]>] [-Prerelease] [-Repository <String[]>] [-Credential <PSCredential>]
 [-Scope <String>] [-NoClobber] [-TrustRepository] [-Reinstall] [-Quiet] [-AcceptLicense]
 [-RequiredResourceFile <String>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RequiredResourceParameterSet
```
Install-PSResource [-RequiredResource <Object>] [-WhatIf] [-Confirm] [<CommonParameters>]
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

### -AcceptLicense
{{ Fill AcceptLicense Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
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
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

```yaml
Type: System.Management.Automation.PSCredential
Parameter Sets: RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -InputObject
{{ Fill InputObject Description }}

```yaml
Type: System.Object[]
Parameter Sets: InputObjectSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Name
{{ Fill Name Description }}

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -NoClobber
{{ Fill NoClobber Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Prerelease
{{ Fill Prerelease Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Quiet
{{ Fill Quiet Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Reinstall
{{ Fill Reinstall Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
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
Type: System.String[]
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequiredResource
{{ Fill RequiredResource Description }}

```yaml
Type: System.Object
Parameter Sets: RequiredResourceParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequiredResourceFile
{{ Fill RequiredResourceFile Description }}

```yaml
Type: System.String
Parameter Sets: RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scope
{{ Fill Scope Description }}

```yaml
Type: System.String
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:
Accepted values: CurrentUser, AllUsers

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TrustRepository
{{ Fill TrustRepository Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
{{ Fill Type Description }}

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Version
{{ Fill Version Description }}

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

### System.Object[]

### System.Management.Automation.PSCredential

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS

[<add>](<add>)


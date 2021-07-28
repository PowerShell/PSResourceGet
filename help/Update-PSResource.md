---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Update-PSResource

## SYNOPSIS
Updates a package already installed on the user's machine.

## SYNTAX

### NameParameterSet (Default)
```
Update-PSResource [-Name] <String[]> [-Version <String>] [-Prerelease] [-Repository <String[]>]
 [-Scope <Microsoft.PowerShell.PowerShellGet.UtilClasses.ScopeType>] [-TrustRepository] [-Credential <PSCredential>] [-Quiet] [-AcceptLicense] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
The Update-PSResource cmdlet replaces the Update-Module and Update-Script cmdlets from V2.
It updates an already installed package based on the -Name parameter argument.
It does not return an object. Other parameters allow the package to be updated to be further filtered.

## EXAMPLES

### Example 1
```powershell
PS C:\> Get-InstalledPSResource -Name "TestModule"
        Name                                    Version                         Prerelease   Description
        ----                                    -------                         ----------   -----------
        TestModule                              1.2.0                                        test

        Update-PSResource -Name "TestModule"
        Get-InstalledPSResource -Name "TestModule"
        Name                                    Version                         Prerelease   Description
        ----                                    -------                         ----------   -----------
        TestModule                              1.3.0                                        test
        TestModule                              1.2.0                                        test

```

In this example, the user already has the TestModule package installed and they update the package. Update-PSResource will install the latest version of the package without deleting the older version installed.

## PARAMETERS

### -AcceptLicense
For resources that require a license, AcceptLicense automatically accepts the license agreement during the update.

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

### -Credential
Specifies optional credentials to be used when accessing a private repository.

```yaml
Type: System.Management.Automation.PSCredential
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Specifies name of a resource or resources to update.

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: 0
Default value: "*"
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: True
```

### -Prerelease
When specified, allows updating to a prerelease version.

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

### -Quiet
Supresses progress information.

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
Specifies one or more repository names to update packages from.
If not specified, search for packages to update will include all currently registered repositories in order of highest priority.

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

### -Scope
Specifies the scope of the resource to update.

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

### -TrustRepository
Specifies optional credentials to be used when accessing a private repository.

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

### -Version
Specifies the version the resource is to be updated to.

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

## OUTPUTS

## NOTES

## RELATED LINKS

[<add>](<add>)

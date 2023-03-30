---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.date: 02/01/2023
schema: 2.0.0
---

# Unregister-PSResourceRepository

## SYNOPSIS

Removes a registered repository from the local machine.

## SYNTAX

```
Unregister-PSResourceRepository [-Name] <String[]> [-PassThru] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## DESCRIPTION

The cmdlet removes a registered repository from the the local machine.

## EXAMPLES

### Example 1

In this example removes the `PSGv3` repository from the local machine.

```powershell
Get-PSResourceRepository
```

```Output
Name      Uri                                      Trusted Priority
----      ---                                      ------- --------
PSGallery https://www.powershellgallery.com/api/v2 True    10
Local     file:///D:/PSRepoLocal/                  True    20
PSGv3     https://www.powershellgallery.com/api/v3 True    50
```

```powershell
Unregister-PSResourceRepository -Name PSGv3
Get-PSResourceRepository
```

```Output
Name      Uri                                      Trusted Priority
----      ---                                      ------- --------
PSGallery https://www.powershellgallery.com/api/v2 True    10
Local     file:///D:/PSRepoLocal/                  True    20
```

### Example 2

This example shows how to remove multiple registered repositories in a single command. The **Name**
parameter accepts an array containing the names of the repositories to remove.

```powershell
Get-PSResourceRepository
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PoshTestGallery  https://www.poshtestgallery.com/api/v2          True         40
PSGallery        https://www.powershellgallery.com/api/v2       False         50
psgettestlocal   file:///c:/code/testdir                         True         50
```

```powershell
Unregister-PSResourceRepository -Name PoshTestGallery, psgettestlocal
Get-PSResourceRepository
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PSGallery        https://www.powershellgallery.com/api/v2       False         50
```

## PARAMETERS

### -Name

The name of one or more repositories to remove.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -PassThru

When specified, outputs a **PSRepositoryInfo** object for each repository that's removed.

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

### System.String[]

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSRepositoryInfo

By default, the cmdlet doesn't return any objects. When the **PassThru** parameter is used, the
cmdlet outputs a **PSRepositoryInfo** object for each repository that's removed.

## NOTES

## RELATED LINKS

[Register-PSResourceRepository](Register-PSResourceRepository.md)

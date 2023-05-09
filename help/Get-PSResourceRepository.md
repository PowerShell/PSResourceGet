---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.custom: v3-beta20
ms.date: 04/03/2023
schema: 2.0.0
---

# Get-PSResourceRepository

## SYNOPSIS

Finds and returns registered repository information.

## SYNTAX

```
Get-PSResourceRepository [[-Name] <String[]>] [<CommonParameters>]
```

## DESCRIPTION

This cmdlet searches for PowerShell resource repositories that are registered on the machine. By
default, it returns all registered repositories.

## EXAMPLES

### Example 1

This example returns all the repositories registered on the machine.

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

### Example 2

This example uses the **Name** parameter to get a specific repository.

```powershell
Get-PSResourceRepository -Name PSGallery
```

```Output
Name         Uri                                        Trusted   Priority
----         ---                                        -------   --------
PSGallery    https://www.powershellgallery.com/api/v2     False         50
```

### Example 3

This example uses the **Name** parameter to get all repositories that end with `Gallery`.

```powershell
Get-PSResourceRepository -Name "*Gallery"
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PoshTestGallery  https://www.poshtestgallery.com/api/v2          True         40
PSGallery        https://www.powershellgallery.com/api/v2       False         50
```

### Example 4

This example uses the **Name** parameter to get a list of named respositories.

```powershell
Get-PSResourceRepository -Name "PSGallery","PoshTestGallery"
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PoshTestGallery  https://www.poshtestgallery.com/api/v2          True         40
PSGallery        https://www.powershellgallery.com/api/v2       False         50
```

## PARAMETERS

### -Name

The name of the repository to search for. Wildcards are supported. Tab completion for this parameter
cycles through the registered repository names.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSRepositoryInfo

## NOTES

## RELATED LINKS

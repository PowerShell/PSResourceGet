---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# Unregister-PSResourceRepository

## SYNOPSIS
Un-registers a repository from the repository store.

## SYNTAX

### NameParameterSet
```
Unregister-PSResourceRepository [-Name] <String[]> [-PassThru][-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
The Unregister-PSResourceRepository cmdlet unregisters a repository.

## EXAMPLES

### Example 1
```
PS C:\> Get-PSResourceRepository -Name "PoshTestGallery"
PS C:\> Unregister-PSResourceRepository -Name "PoshTestGallery"
PS C:\> Get-PSResourceRepository -Name "PoshTestGallery"
PS C:\>

```

In this example, we assume the repository "PoshTestGallery" has been previously registered. So when we first run the command to find "PoshTestGallery" it verifies that this repository can be found. Next, we run the command to unregister "PoshTestGallery". Finally, we again run the command to find "PoshTestGallery" but since it was successfully un-registered it cannot be found or retrieved.

### Example 2
```
PS C:\> Get-PSResourceRepository
        Name             Url                                          Trusted   Priority
        ----             ---                                          -------   --------
        PoshTestGallery  https://www.poshtestgallery.com/api/v2          True         40
        PSGallery        https://www.powershellgallery.com/api/v2       False         50
        psgettestlocal   file:///c:/code/testdir                         True         50

PS C:\> Unregister-PSResourceRepository -Name "PoshTestGallery","psgettestlocal"
PS C:\> Get-PSResourceRepository
        Name             Url                                          Trusted   Priority
        ----             ---                                          -------   --------
        PSGallery        https://www.powershellgallery.com/api/v2       False         50

```

In this example, the command to find all registered repositories is run and the repositories found are displayed. Next, the command to un-register is run with a list of names ("PoshTestGallery", "psgettestlocal") provided for the `-Name` parameter. Finally, the command to find all registered repositories is run again, but this time we can see that "PoshTestGallery" and "psgettestlocal" are not found and displayed as they have been successfully unregistered.

## PARAMETERS

### -Name
This parameter takes a String argument, or an array of String arguments. It is the name of the repository to un-register.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -PassThru
Passes the resource installed to the console.

```yaml
Type: SwitchParameter
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
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]

## OUTPUTS

None

## NOTES

## RELATED LINKS

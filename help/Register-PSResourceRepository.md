---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# Register-PSResourceRepository

## SYNOPSIS
Registers a repository for PowerShell resources.

## SYNTAX

### NameParameterSet (Default)
```
Register-PSResourceRepository [-Name] <String> [-Uri] <String> [-Trusted] [-Priority <Int32>] [-PassThru]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### PSGalleryParameterSet
```
Register-PSResourceRepository [-PSGallery] [-Trusted] [-Priority <Int32>] [-PassThru] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### RepositoriesParameterSet
```
Register-PSResourceRepository -Repository <Hashtable[]> [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
The Register-PSResourceRepository cmdlet registers a repository for PowerShell resources.

## EXAMPLES
These examples assume that the repository we attempt to register is not already registered on the user's machine.
### Example 1
```
PS C:\> Register-PSResourceRepository -Name "PoshTestGallery" -Uri "https://www.powershellgallery.com/api/v2"
PS C:\> Get-PSResourceRepository -Name "PoshTestGallery"
        Name             Uri                                          Trusted   Priority
        ----             ---                                          -------   --------
        PoshTestGallery  https://www.poshtestgallery.com/api/v2         False         50
```

This example registers the repository with the `-Name` of "PoshTestGallery" along with the associated `Uri` value for it.

### Example 2
```
PS C:\> Register-PSResourceRepository -PSGallery
PS C:\> Get-PSResourceRepository -Name "PSGallery"
        Name             Uri                                          Trusted   Priority
        ----             ---                                          -------   --------
        PSGallery        https://www.powershellgallery.com/api/v2       False         50
```

This example registers the "PSGallery" repository, with the 'PSGallery' parameter. Unlike the previous example, we cannot use the `-Name` or `-Uri` parameters to register the "PSGallery" repository as it is considered Powershell's default repository store and has its own value for Uri.

### Example 3
```
PS C:\> $arrayOfHashtables = @{Name = "psgettestlocal"; Uri = "c:/code/testdir"}, @{PSGallery = $True}
PS C:\> Register-PSResourceRepository -Repository $arrayOfHashtables
PS C:\> Get-PSResourceRepository
        Name             Uri                                          Trusted   Priority
        ----             ---                                          -------   --------
        PSGallery        https://www.powershellgallery.com/api/v2       False         50
        psgettestlocal   file:///c:/code/testdir                        False         50

```

This example registers multiple repositories at once. To do so, we use the `-Repository` parameter and provide an array of hashtables. Each hashtable can only have keys associated with parameters for the NameParameterSet or the PSGalleryParameterSet. Upon running the command we can see that the "psgettestlocal" and "PSGallery" repositories have been succesfully registered.

## PARAMETERS

### -Name
Name of the repository to be registered.
Cannot be "PSGallery".

```yaml
Type: String
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Priority
Specifies the priority ranking of the repository.
Repositories with higher ranking priority are searched before a lower ranking priority one, when searching for a repository item across multiple registered repositories. Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds to a higher priority ranking than a higher numeric value (i.e 40). Has default value of 50.

```yaml
Type: Int32
Parameter Sets: NameParameterSet, PSGalleryParameterSet
Aliases:

Required: False
Position: Named
Default value: 50
Accept pipeline input: False
Accept wildcard characters: False
```

### -PSGallery
When specified, registers PSGallery repository.

```yaml
Type: SwitchParameter
Parameter Sets: PSGalleryParameterSet
Aliases:

Required: True
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository
Specifies an array of hashtables which contains repository information and is used to register multiple repositories at once.

```yaml
Type: Hashtable[]
Parameter Sets: RepositoriesParameterSet
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Trusted
Specifies whether the repository should be trusted.

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

### -Uri
Specifies the location of the repository to be registered.
Uri can be of the following Uri schemas: HTTPS, HTTP, FTP, file share based.

```yaml
Type: String
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: 1
Default value: None
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

### -PassThru
When specified, displays the succcessfully registered repository and its information.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

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

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSRepositoryInfo (if 'PassThru' parameter is used)

## NOTES
Repositories are unique by 'Name'. Attempting to register a repository with same 'Name' as an already registered repository will not successfully register.

Registering the PSGallery repository must be done via the PSGalleryParameterSet (i.e by using the 'PSGallery' parameter instead of 'Name' and 'Uri' parameters).

Uri string input must be of one of the following Uri schemes: HTTP, HTTPS, FTP, File

## RELATED LINKS

---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
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
The Get-PSResourceRepository cmdlet searches for the PowerShell resource repositories that are registered on the machine. By default it will return all registered repositories, or if the `-Name` parameter argument is specified then it will return the repository which matches that name. It returns PSRepositoryInfo objects which contain information for each repository item found.

## EXAMPLES

### Example 1
```
PS C:\> Get-PSResourceRepository -Name "PSGallery"
        Name         Url                                        Trusted   Priority
        ----         ---                                        -------   --------
        PSGallery    https://www.powershellgallery.com/api/v2     False         50
```

This example runs the command with the 'Name' parameter being set to "PSGallery". This repository is registered on this machine so the command returns information on this repository.

### Example 2
```
PS C:\> Get-PSResourceRepository -Name "*Gallery"
        Name             Url                                          Trusted   Priority
        ----             ---                                          -------   --------
        PoshTestGallery  https://www.poshtestgallery.com/api/v2          True         40
        PSGallery        https://www.powershellgallery.com/api/v2       False         50

```

This example runs the command with the 'Name' parameter being set to "*Gallery" which includes a wildcard. The following repositories are registered on this machine and match the name pattern, so the command returns information on these repositories.

### Example 3
```
PS C:\> Get-PSResourceRepository -Name "PSGallery","PoshTestGallery"
        Name             Url                                          Trusted   Priority
        ----             ---                                          -------   --------
        PoshTestGallery  https://www.poshtestgallery.com/api/v2          True         40
        PSGallery        https://www.powershellgallery.com/api/v2       False         50

```

This example runs the command with the 'Name' parameter being set to an array of Strings. Both of the specified repositories are registered on this machine and match the name pattern, so the command returns information on these repositories.

### Example 4
```
PS C:\> Get-PSResourceRepository -Name "*"
        Name             Url                                          Trusted   Priority
        ----             ---                                          -------   --------
        PoshTestGallery  https://www.poshtestgallery.com/api/v2          True         40
        PSGallery        https://www.powershellgallery.com/api/v2       False         50
        psgettestlocal   file:///c:/code/testdir                         True         50

```

This example runs the command with the 'Name' parameter being set to a single wildcard character. So all the repositories registered on this machine are returned.

## PARAMETERS

### -Name
This parameter takes a String argument, including wildcard characters, or an array of such String arguments. It is used to search for repository names from the repository store which match the provided name pattern. Tab completion is provided on this argument and will display registered repository names.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSRepositoryInfo

## NOTES
If no value for Name is provided, Get-PSResourceRepository will return information for all registered repositories.

## RELATED LINKS

---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# Set-PSResourceRepository

## SYNOPSIS
Sets information for a registered repository.

## SYNTAX

### NameParameterSet (Default)
```
Set-PSResourceRepository [-Name] <String> [-Uri <String>][CredentialInfo <PSCredentialInfo>] [-Trusted] [-Priority <Int32>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RepositoriesParameterSet
```
Set-PSResourceRepository -Repository <Hashtable[]> [-Priority <Int32>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
The Set-PSResourceRepository cmdlet sets information for a registered repository.

## EXAMPLES
These examples are run independently of each other and assume the repositories used are already registered. The `-PassThru` parameter used with Set-PSResourceRepository is only used to display the changes made to the repository and is not mandatory.
### Example 1
```powershell
PS C:\> Get-PSResourceRepository -Name "PoshTestGallery"
        Name             Uri                                          Trusted   Priority
        ----             ---                                          -------   --------
        PoshTestGallery  https://www.poshtestgallery.com/api/v2         False         50
PS C:\> Set-PSResourceRepository -Name "PoshTestGallery" -Uri "c:/code/testdir" -PassThru
        Name             Uri                                          Trusted   Priority
        ----             ---                                          -------   --------
        PoshTestGallery  file:///c:/code/testdir                        False         50
```

This example first checks if the PoshTestGallery repository has been registered. We wish to set the `-Uri` value of this repository by running the Set-PSResourceRepository cmdlet with the `-Uri` parameter and a valid Uri scheme Uri. We run the Get-PSResourceRepository cmdlet again to ensure that the `-Uri` of the repository was changed. We also use the `-PassThru` parameter to see the changed repository.

### Example 2
```powershell
PS C:\> Get-PSResourceRepository -Name "PSGallery"
        Name             Uri                                          Trusted   Priority
        ----             ---                                          -------   --------
        PSGallery        https://www.powershellgallery.com/api/v2       False         50
PS C:\> Set-PSResourceRepository -Name "PSGallery" -Priority 25 -Trusted -PassThru
        Name             Uri                                          Trusted   Priority
        ----             ---                                          -------   --------
        PSGallery        https://www.powershellgallery.com/api/v2        True         25
```

This example first checks if the PSGallery repository has been registered. We wish to set the `-Priority` and `-Trusted` values of this repository by running the Set-PSResourceRepository cmdlet with the `-Priority` parameter set to a value between 0 and 50 and by using the `-Trusted` parameter switch. We run the Get-PSResourceRepository cmdlet again to ensure that the `-Priority` and `-Trusted` values of the repository were changed. An important note here is that just for the default PSGallery repository, the `-Uri` value can't be changed/set. We also use the `-PassThru` parameter to see the changed repository.

### Example 3
```powershell
PS C:\> Get-PSResourceRepository -Name "*"
        Name             Uri                                          Trusted   Priority
        ----             ---                                          -------   --------
        PSGallery        https://www.powershellgallery.com/api/v2       False         50
        PoshTestGallery  https://www.poshtestgallery.com/api/v2         False         50

PS C:\> $arrayOfHashtables = @{Name = "PSGallery"; Trusted = $True}, @{Name = "PoshTestGallery"; Uri = "c:/code/testdir"}

PS C:\> Set-PSResourceRepository -Repository $arrayOfHashtables -PassThru
        Name             Uri                                          Trusted   Priority
        ----             ---                                          -------   --------
        PSGallery        https://www.powershellgallery.com/api/v2        True         50
        PoshTestGallery  file:///c:/code/testdir                        False         50
```

This example first checks for all registered repositories. We wish to set the properties for multiple repositories at once (i.e the PSGallery and PoshTestGallery repositories), so we run Set-PSResourceRepository with the `-Repository` parameter. This parameter takes an array of hashtables, where each hashtable contains information for a repository we wish to set information for. We also use the `-PassThru` parameter to see the changed repositories.

## PARAMETERS

### -Name
Specifies the name of the repository to be set.

```yaml
Type: System.String
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Priority
Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched before a lower ranking priority one, when searching for a repository item across multiple registered repositories. Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds to a higher priority ranking than a higher numeric value (i.e 40).

```yaml
Type: System.Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Trusted
Specifies whether the repository should be trusted.

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

### -Uri
Specifies the location of the repository to be set.

```yaml
Type: String
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```
### -CredentialInfo
Specifies where a credential is stored to access the PSResourceRepository for Find/Install/Update commands.
Takes a PSCredentialInfo Objects which takes in a vault name and secret name.
This parameter utilizes the Microsoft.PowerShell.SecretManagement module for interfacing with the stored credential.

`New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("VaultName", "SecretName")`

```yaml
Type: PSCredentialInfo
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
When specified, displays the succcessfully registered repository and its information

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

### System.Collections.Hashtable[]

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSRepositoryInfo (if 'PassThru' parameter used)

## NOTES

## RELATED LINKS

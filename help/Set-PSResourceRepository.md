---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.date: 07/27/2022
schema: 2.0.0
---

# Set-PSResourceRepository

## SYNOPSIS
Sets information for a registered repository.

## SYNTAX

### NameParameterSet (Default)

```
Set-PSResourceRepository [-Name] <string> [-Uri <string>] [-Trusted] [-Priority <int>] 
 [-CredentialInfo <PSCredentialInfo>] [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RepositoriesParameterSet

```
Set-PSResourceRepository -Repository <hashtable[]> [-PassThru] [-WhatIf] [-Confirm] 
 [<CommonParameters>]
```

## DESCRIPTION

The Set-PSResourceRepository cmdlet sets information for a registered repository.

## EXAMPLES

### Example 1

In this example, the **Uri** for the **PoshTestGallery** repository has been registered. The
`Set-PSResourceRepository` cmdlet is used to change the **Uri** to a local path. The **PassThru**
parameter allows you to see the changed repository.

```powershell
Get-PSResourceRepository -Name "PoshTestGallery"
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PoshTestGallery  https://www.poshtestgallery.com/api/v2         False         50
```

```powershell
Set-PSResourceRepository -Name "PoshTestGallery" -Uri "c:/code/testdir" -PassThru
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PoshTestGallery  file:///c:/code/testdir                        False         50
```

### Example 2

This example changes the **Priority** and **Trusted** values of the repository. were changed.

> [!NOTE]
> The **Uri** value of the default **PSGallery** repository can't be changed.

```powershell
Get-PSResourceRepository -Name "PSGallery"
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PSGallery        https://www.powershellgallery.com/api/v2       False         50

Set-PSResourceRepository -Name "PSGallery" -Priority 25 -Trusted -PassThru
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PSGallery        https://www.powershellgallery.com/api/v2        True         25
```

### Example 3

This example uses the **Repository** parameter to change values for multiple respositories. The
parameter takes an array of hashtables. Each hashtable contains information the repository being
updated.

```powershell
Get-PSResourceRepository
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PSGallery        https://www.powershellgallery.com/api/v2       False         50
PoshTestGallery  https://www.poshtestgallery.com/api/v2         False         50
```

```powershell
$arrayOfHashtables = @{Name = "PSGallery"; Trusted = $True},
                     @{Name = "PoshTestGallery"; Uri = "c:/code/testdir"}
Set-PSResourceRepository -Repository $arrayOfHashtables -PassThru
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PSGallery        https://www.powershellgallery.com/api/v2        True         50
PoshTestGallery  file:///c:/code/testdir                        False         50
```

### Example 4

This example updates a repository with credential information to be retrieved from a registered
**Microsoft.PowerShell.SecretManagement** vault. You must have the
**Microsoft.PowerShell.SecretManagement** module install and have a registered vault containing the
stored secret. The format of the secret must match the requirements of the repository.

```powershell
$parameters = @{
    Name = "PoshTestGallery"
    Uri = "c:/code/testdir"
    CredentialInfo = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ('SecretStore', 'TestSecret')
}
Set-PSResourceRepository @parameters -PassThru | Select-Object * -ExpandProperty CredentialInfo
```

```Output
Name           : PoshTestGallery
Uri            : file:///c:/code/testdir
Trusted        : False
Priority       : 50
CredentialInfo : Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo
VaultName      : SecretStore
SecretName     : TestSecret
Credential     :
```

## PARAMETERS

### -CredentialInfo

A **PSCredentialInfo** object that includes the name of a vault and a secret that is stored in a
**Microsoft.PowerShell.SecretManagement** store.

```yaml
Type: Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name

Specifies the name of the repository to be modified.

> [!NOTE]
> The **Uri** value of the default **PSGallery** repository can't be changed.


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

### -PassThru

When specified, displays the successfully registered repository and its information.

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

### -Priority

Specifies the priority ranking of the repository. Valid priority values range from 0 to 50. Lower
values have a higher priority ranking. The default value is `50`.

Repositories are searched in priority order (highest first).

```yaml
Type: System.Int32
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository

Specifies an array of hashtables that contain repository information. Use this parameter to register
multiple repositories at once. Each hashtable can only have keys associated with parameters for
the **NameParameterSet**.

```yaml
Type: System.Collections.Hashtable[]
Parameter Sets: RepositoriesParameterSet
Aliases:

Required: True
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
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Uri

Specifies the location of the repository to be registered. The value must use one of the following
URI schemas:

- `https://`
- `http://`
- `ftp://`
- `file://`

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

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose,
-WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String

### System.Collections.Hashtable[]

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSRepositoryInfo

By default, the cmdlet produces no output. When you use the **PassThru** parameter, the cmdlet
returns a **PSRepositoryInfo** object.

## NOTES

## RELATED LINKS

[Microsoft.PowerShell.SecretManagement](/powershell/utility-modules/secretmanagement/overview)

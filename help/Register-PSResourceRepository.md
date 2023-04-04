---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.custom: v3-beta20
ms.date: 04/03/2023
schema: 2.0.0
---

# Register-PSResourceRepository

## SYNOPSIS

Registers a repository for PowerShell resources.

## SYNTAX

### NameParameterSet (Default)

```
Register-PSResourceRepository [-Name] <string> [-Uri] <string> [-Trusted] [-Priority <int>]
 [-CredentialInfo <PSCredentialInfo>] [-PassThru] [-Force] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### PSGalleryParameterSet

```
Register-PSResourceRepository -PSGallery [-Trusted] [-Priority <int>] [-PassThru] [-Force]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RepositoriesParameterSet

```
Register-PSResourceRepository -Repository <hashtable[]> [-PassThru] [-Force] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## DESCRIPTION

The cmdlet registers a NuGet repository containing PowerShell resources.

## EXAMPLES

### Example 1

This example registers the repository with the **Name** of `PoshTestGallery`.

```powershell
Register-PSResourceRepository -Name PoshTestGallery -Uri 'https://www.poshtestgallery.com/api/v2'
Get-PSResourceRepository -Name PoshTestGallery
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PoshTestGallery  https://www.poshtestgallery.com/api/v2         False         50
```

### Example 2

This example registers the default `PSGallery` repository. Unlike the previous example, we can't use
the **Name** and **Uri** parameters to register the `PSGallery` repository. The `PSGallery`
repository is registered by default but can be removed. Use this command to restore the default
registration.

```powershell
Register-PSResourceRepository -PSGallery
Get-PSResourceRepository -Name 'PSGallery'
```

```Output
Name             Uri                                          Trusted   Priority
----             ---                                          -------   --------
PSGallery        https://www.powershellgallery.com/api/v2       False         50
```

### Example 3

This example registers multiple repositories at once. To do so, we use the **Repository** parameter
and provide an array of hashtables. Each hashtable can only have keys associated with parameters for
the **NameParameterSet** or the **PSGalleryParameterSet**.

```powershell
$arrayOfHashtables = @{
        Name = 'Local'
        Uri = 'D:/PSRepoLocal/'
        Trusted = $true
        Priority = 20
    },
    @{
        Name = 'PSGv3'
        Uri = 'https://www.powershellgallery.com/api/v3'
        Trusted = $true
        Priority = 50
    },
    @{
        PSGallery = $true
        Trusted = $true
        Priority = 10
    }
Register-PSResourceRepository -Repository $arrayOfHashtables
Get-PSResourceRepository
```

```Output
Name      Uri                                      Trusted Priority
----      ---                                      ------- --------
PSGallery https://www.powershellgallery.com/api/v2 True    10
Local     file:///D:/PSRepoLocal/                  True    20
PSGv3     https://www.powershellgallery.com/api/v3 True    50
```

### Example 4

This example registers a repository with credential information to be retrieved from a registered
**SecretManagement** vault. You must have the **Microsoft.PowerShell.SecretManagement** module
installed and have a registered vault containing the stored secret. The format of the secret must
match the requirements of the repository.

```powershell
$parameters = @{
    Name = 'PSGv3'
    Uri = 'https://www.powershellgallery.com/api/v3'
    Trusted = $true
    Priority = 50
    CredentialInfo = [Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo]::new('SecretStore', 'TestSecret')
}
Register-PSResourceRepository @parameters
Get-PSResourceRepository | Select-Object * -ExpandProperty CredentialInfo
```

```Output
Name           : PSGv3
Uri            : https://www.powershellgallery.com/api/v3
Trusted        : True
Priority       : 50
CredentialInfo : Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo
VaultName      : SecretStore
SecretName     : TestSecret
Credential     :
```

## PARAMETERS

### -CredentialInfo

A **PSCredentialInfo** object that includes the name of a vault and a secret that's stored in a
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

Name of the repository to be registered. Can't be `PSGallery`.

```yaml
Type: System.String
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
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

Specifies the priority ranking of the repository. Valid priority values range from 0 to 100. Lower
values have a higher priority ranking. The default value is `100`.

Repositories are searched in priority order (highest first).

```yaml
Type: System.Int32
Parameter Sets: NameParameterSet, PSGalleryParameterSet
Aliases:

Required: False
Position: Named
Default value: 50
Accept pipeline input: False
Accept wildcard characters: False
```

### -PSGallery

When specified, registers **PSGallery** repository.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: PSGalleryParameterSet
Aliases:

Required: True
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository

Specifies an array of hashtables that contain repository information. Use this parameter to register
multiple repositories at once. Each hashtable can only have keys associated with parameters for
the **NameParameterSet** or the **PSGalleryParameterSet**.

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
Parameter Sets: NameParameterSet, PSGalleryParameterSet
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

Required: True
Position: 1
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

### -Force

Overwrites a repository if it already exists.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose,
-WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSRepositoryInfo

By default, the cmdlet produces no output. When you use the **PassThru** parameter, the cmdlet
returns a **PSRepositoryInfo** object.

## NOTES

Repositories are unique by **Name**. Attempting to register a repository with same name results in
an error.

## RELATED LINKS

[Microsoft.PowerShell.SecretManagement](/powershell/utility-modules/secretmanagement/overview)

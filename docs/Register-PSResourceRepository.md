---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Register-PSResourceRepository

## SYNOPSIS

Registers a PowerShell repository.

## SYNTAX

### NameParameterSet (Default)
```
Register-PSResourceRepository [-Name] <String> [-URL] <Uri> [-Credential <PSCredential>] [-Trusted]
 [-Proxy <Uri>] [-ProxyCredential <PSCredential>] [-Priority <Int32>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### PSGalleryParameterSet
```
Register-PSResourceRepository [-PSGallery] [-Trusted] [-Proxy <Uri>] [-ProxyCredential <PSCredential>]
 [-Priority <Int32>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RepositoriesParameterSet
```
Register-PSResourceRepository -Repositories <System.Collections.Generic.List`1[System.Collections.Hashtable]>
 [-Proxy <Uri>] [-ProxyCredential <PSCredential>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION

The `Register-PSResourceRepository` cmdlet registers a repository for PowerShell modules. After a
repository is registered, you can reference it from the `Find-PSResource`, `Install-PSResource`, and
`Publish-PSResource` cmdlets. 

Registered repositories are user-specific. They are not registered in a system-wide context.

## EXAMPLES

### Example 1: Register a repository

```powershell
$parameters = @{
  Name = "myNuGetSource"
  uri = "https://www.myget.org/F/powershellgetdemo/api/v2"
  Priority = 10
  InstallationPolicy = 'Trusted'
}
Register-PSResourceRepository @parameters
```

## PARAMETERS

### -Credential

Specifies credentials of an account that has rights to register a repository.

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

### -Name

Specifies the name of the repository to register. You can use this name to specify the repository in
cmdlets such as `Find-PSResource` and `Install-PSResource`.

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

### -Priority

Specifies the search order of repositories with a lower value indicating a higher priority. If not specified, the default value is 50. The PSGallery, which is registered by default, has an editable value of 50. If two PSRepositories have the same priority the “Trusted” one will be chosen, if they also have the same level of trust the first one alphabetically will be selected.

```yaml
Type: System.Int32
Parameter Sets: NameParameterSet, PSGalleryParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy

Specifies a proxy server for the request, rather than connecting directly to the Internet resource.


```yaml
Type: System.Uri
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -ProxyCredential

Specifies a user account that has permission to use the proxy server that is specified by the
**Proxy** parameter.

```yaml
Type: System.Management.Automation.PSCredential
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -PSGallery

Registers the PSGallery with default settings.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: PSGalleryParameterSet
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repositories

Specifies a hashtable of repositries to register.

```yaml
Type: System.Collections.Generic.List`1[System.Collections.Hashtable]
Parameter Sets: RepositoriesParameterSet
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Trusted

Specifies the installation policy as trusted. When installing modules from an UnTrusted repository, the user is prompted for
confirmation.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, PSGalleryParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -URL

Specifies the URI for discovering, installing, and publishing recources from this repository. A URI can be a NuGet
server feed (most common situation), HTTP, HTTPS, FTP or file location.

```yaml
Type: System.Uri
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

### System.Management.Automation.PSCredential

### System.Uri

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS

[Get-PSResourceRepository](Get-PSResourceRepository.md)

[Set-PSResourceRepository](Set-PSResourceRepository.md)

[Unregister-PSResourceRepository](Unregister-PSResourceRepository.md)


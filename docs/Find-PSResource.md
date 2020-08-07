---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Find-PSResource

## SYNOPSIS
Finds PowerShell resources in a repository that match specified criteria.

## SYNTAX

```
Find-PSResource [[-Name] <String[]>] [-Type <String[]>] [-Version <String>] [-Prerelease]
 [-ModuleName <String>] [-Tags <String[]>] [-Repository <String[]>] [-Credential <PSCredential>]
 [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION

The `Find-PSResource` cmdlet finds PowerShell resources in a repository that match the specified criteria.
`Find-PSResource` returns a **PSRepositoryItemInfo** object for each module it finds. The objects can be
sent down the pipeline to cmdlets such as `Install-PSResource`.

If the repository source is not registered with `Register-PSResourceRepository` cmdlet, an error is
returned.

`Find-Module` returns the newest version of a module if no parameters are used that limit the
version. To get a repository's list of a module's versions, use the wilcard `*` with the parameter **Version**.

The following examples use the [PowerShell Gallery](https://www.powershellgallery.com/) as the only
registered repository. `Get-PSResourceRepository` displays the registered repositories. If you have multiple
registered repositories, use the `-Repository` parameter to specify the repository's name.

## EXAMPLES

### Example 1

### Example 1: Find a module by name

This example finds a module in the default repository.

```powershell
Find-PSResource -Name PowerShellGet -Type Module
```

```Output
Version   Name              Repository           Description
-------   ----              ----------           -----------
3.0.0     PowerShellGet     PSGallery            PowerShell module with commands for discovering...
```

The `Find-PSResource` cmdlet uses the **Name** parameter to specify the **PowerShellGet** module.

### Example 2: Find a module by minimum version

This example searches for a module's minimum version. If the repository contains a newer version of
the module, the newer version is returned.

```powershell
Find-PSResource -Name PowerShellGet -Version "(1.6.5,*) -Type Module
```

```Output
Version   Name             Repository     Description
-------   ----             ----------     -----------
3.0.0     PowerShellGet    PSGallery      PowerShell module with commands for discovering...
```

The `Find-PSResource` cmdlet uses the **Name** parameter to specify the **PowerShellGet** module. The
**Version** specifies version **1.6.5** or above. `Find-PSResource` returns PowerShellGet version
**3.0.0** because it exceeds the minimum version and is the most current version.

### Example 3: Find a module in a specific repository

This example uses the **Repository** parameter to find a module in a specific repository.

```powershell
Find-PSResource -Name PowerShellGet -Repository PSGallery -Type Module
```

```Output
Version   Name             Repository     Description
-------   ----             ----------     -----------
3.0.0     PowerShellGet    PSGallery      PowerShell module with commands for discovering...
```

The `Find-PSResource` cmdlet uses the **Name** parameter to specify the **PowerShellGet** module. The
**Repository** parameter specifies to search the **PSGallery** repository.

## PARAMETERS

### -Credential

Specifies a user account that has rights to install a module for a specified package provider or
source.

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

### -IncludeDependencies

Indicates that this operation includes all modules that are dependent upon the module specified in
the **Name** parameter.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -ModuleName

Specifies the name of a module to search for commands. The default is all modules.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Name

Specifies the names of resources to search for in the repository. A comma-separated list of names is accepted.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Prerelease

Includes in the results modules marked as a pre-release.

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

Use the **Repository** parameter to specify which repository to search for a module. Used when
multiple repositories are registered. Accepts a comma-separated list of repositories. To register a
repository, use `Register-PSRepository`. To display registered repositories, use `Get-PSRepository`.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Tags

Specifies an array of tags. Example tags include **DesiredStateConfiguration**, **DSC**,
**DSCResourceKit**, or **PSModule**.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Type

Specifies a type of PSResource to search for.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:
Accepted values: Module, Script, DscResource, RoleCapability, Command

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Version

Specifies the version or version range to include in the results. 

```yaml
Type: System.String
Parameter Sets: (All)
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

### System.String

### System.Management.Automation.PSCredential

### System.Management.Automation.SwitchParameter

## OUTPUTS

### PSRepositoryItemInfo

`Find-PSResource` creates **PSRepositoryItemInfo** objects that can be sent down the pipeline to cmdlets
such as `Install-PSResource`.

## NOTES

This cmdlet runs on PowerShell 5.0 or later releases of PowerShell, on Windows 7, or Windows
2008 R2 and later releases of Windows.

## RELATED LINKS

[Get-PSResourceRepository](Get-PSResourceRepository.md)

[Install-PSResource](Install-PSResource.md)

[Publish-PSResource](Publish-PSResource.md)

[Save-PSResource](Save-PSResource.md)

[Uninstall-PSResource](Uninstall-PSResource.md)

[Update-PSResource](Update-PSResource.md)

[Register-PSPSResourceRepository](Register-PSPSResourceRepository.md)


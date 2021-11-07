---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Publish-PSResource

## SYNOPSIS
Publishes a specified module from the local computer to PSResource repository.

## SYNTAX

### PathParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-Path] <String>
 [-Credential <PSCredential>] [-SkipDependenciesCheck]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### NameParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] -Name <String>
 [-Credential <PSCredential>] [-SkipDependenciesCheck]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
The Publish-PSResource cmdlet combines the Publish-Module and Publish-Script cmdlets from V2.
It publishes a specified resource from the local computer to an online Nuget-based gallery by using an API key, stored as part of a user's profile in the gallery or to a local repository. 
You can specify the resource to publish either by the resource's name, or by the path to the folder containing the module or script resource.

## EXAMPLES

### Example 1
```powershell
PS C:\> Publish-PSResource -Path c:\Test-Module
```

This will publish the module 'Test-Module' to the highest priority repository

### Example 2
```powershell
PS C:\> Publish-PSResource -Path c:\Test-Module -Repository PSGallery -APIKey '1234567'
```

This will publish the module 'Test-Module' to the PowerShellGallery.  Note that the API key is a secret that is generated for a user from the website itself.

## PARAMETERS

### -APIKey
Specifies the API key that you want to use to publish a resource to the online gallery.

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository
Specifies the repository to publish to.

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Specifies the path to the resource that you want to publish. This parameter accepts the path to the folder that contains the module or the full path of the script.

```yaml
Type: System.String
Parameter Sets: PathParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Name
Specifies the name of the module that you want to publish. Searches for the specified module name in $Env:PSModulePath.
This parameter can only be used to publish modules.

```yaml
Type: System.String
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Credential
Specifies a user account that has rights to a specific repository (used for finding dependencies).

```yaml
Type: System.Management.Automation.PSCredential
Parameter Sets: PathParameterSet, PathLiteralParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipDependenciesCheck
Bypasses the default check that all dependencies are present on the repository which the resource is being published to.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: PathParameterSet, PathLiteralParameterSet
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
Parameter Sets: PathParameterSet, PathLiteralParameterSet
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
Parameter Sets: PathParameterSet, PathLiteralParameterSet
Aliases: wi

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).


## OUTPUTS
None

## NOTES

## RELATED LINKS


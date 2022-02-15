---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# New-ScriptFileInfo

## SYNOPSIS
Updates an existing .ps1 file with requested properties and ensures it's valid.

## SYNTAX

```
Update-PSScriptFileInfo [-FilePath <String>] [-Version <string>] [-Author <string>] [-Description <string>] [-Guid <Guid>] [-CompanyName <string>] [-Copyright <string>>] [-RequiredModules <Microsoft.PowerShell.Commands.ModuleSpecification[]>] [-ExternalModuleDependencies <string[]>] [-RequiredScripts <string[]>] [-ExternalScriptDependencies <string[]>] [-Tags <string[]>] [-ProjectUri <System.Uri>] [-LicenseUri <System.Uri>] [-IconUri <System.Uri>] [-ReleaseNotes <string[]>] [-PrivateData <string>] [-WhatIf] [-Validate] [<CommonParameters>] [<CommonParameters>]
```

## DESCRIPTION
Updates an existing .ps1 script file with the requested properties and ensures it's valid.

## EXAMPLES

## PARAMETERS

### -FilePath
The path the .ps1 script info file will be created at.

```yaml
Type: System.String
Parameter Sets:
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Version
The version of the script.

```yaml
Type: System.String
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Author
The author of the script.

```yaml
Type: System.String
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Description
The description of the script.

```yaml
Type: System.String
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GUID
The GUID for the script.

```yaml
Type: System.String
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CompanyName
The name of the company owning the script.

```yaml
Type: System.String
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Copyright
The copyright information for the script.

```yaml
Type: System.String
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequiredModules
The list of modules required for the script.

```yaml
Type: Microsoft.PowerShell.Commands.ModuleSpecification[]
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExternalModuleDependencies
The list of external module dependencies taken by this script

```yaml
Type: System.String[]
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequiredScripts
The list of scripts required by the script

```yaml
Type: System.String[]
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExternalScriptDependencies
The list of external script dependencies taken by this script

```yaml
Type: System.String[]
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Tags
The tags associated with the script.

```yaml
Type: System.String[]
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProjectUri
The Uri for the project associated with the script

```yaml
Type: System.Uri
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LicenseUri
The Uri for the license associated with the script

```yaml
Type: System.Uri
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IconUri
The Uri for the icon associated with the script

```yaml
Type: System.Uri
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReleaseNotes
The release notes for the script

```yaml
Type: System.String[]
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PrivateData
The private data associated with the script

```yaml
Type: System.String
Parameter Sets:
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo
```
PSScriptFileInfo : {
    Version
    Guid
    Author
    CompanyName
    Copyright
    Tags
    LicenseUri
    ProjectUri
    IconUri
    RequiredModules
    ExternalModuleDependencies
    RequiredScripts
    ExternalScriptDependencies
    ReleaseNotes
    PrivateData
    Description
    Synopsis
    Example
    Inputs
    Outputs
    Notes
    Links
    Component
    Role
    Functionality
}
```

## NOTES

## RELATED LINKS

---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# New-ScriptFileInfo

## SYNOPSIS
Tests a .ps1 file at the specified path to ensure it is valid.

## SYNTAX

```
Tes-PSScriptFileInfo [-Path <String>] [<CommonParameters>]
```

## DESCRIPTION
The Test-PSScriptFileInfo cmdlet tests a .ps1 file at the specified path to ensure it is valid.

## EXAMPLES


## PARAMETERS

### -Path
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

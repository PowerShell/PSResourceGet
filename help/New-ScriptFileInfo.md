---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# New-ScriptFileInfo

## SYNOPSIS
Returns resources (modules and scripts) installed on the machine via PowerShellGet.

## SYNTAX

```
New-ScriptFileInfo [-Path <String>] [<CommonParameters>]
```

## DESCRIPTION
The Get-PSResource cmdlet combines the Get-InstalledModule, Get-InstalledScript cmdlets from V2. It performs a search within module or script installation paths based on the -Name parameter argument. It returns PSResourceInfo objects which describes each resource item found. Other parameters allow the returned results to be filtered by version and path.

## EXAMPLES


## PARAMETERS

### -Path
The path the .ps1 script info file will be created at.

```yaml
Type: System.String
Parameter Sets:
Aliases:

Required: ??
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

Required: ??
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

Required: ??
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

Required: ??
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

Required: ??
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

Required: ??
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
Type: System.String[] # this was Object[] in V2 TODO, determine type
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
PSResourceInfo : {
    AdditionalMetadata
    Author
    CompanyName
    Copyright
    Dependencies
    Description
    IconUri
    Includes
    InstalledDate
    InstalledLocation
    IsPrerelease
    LicenseUri
    Name
    PackageManagementProvider
    PowerShellGetFormatVersion
    Prerelease
    ProjectUri
    PublishedDate
    ReleaseNotes
    Repository
    RepositorySourceLocation
    Tags
    Type
    UpdatedDate
    Version
}
```

## NOTES

## RELATED LINKS

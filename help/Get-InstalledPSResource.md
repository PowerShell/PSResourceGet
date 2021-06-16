---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# Get-InstalledPSResource

## SYNOPSIS
Returns resources (modules and scripts) installed on the machine via PowerShellGet.

## SYNTAX

```
Get-InstalledPSResource [[-Name] <String[]>] [-Version <String>] [-Path <String>] [<CommonParameters>]
```

## DESCRIPTION
The Get-InstalledPSResource cmdlet combines the Get-InstalledModule, Get-InstalledScript cmdlets from V2. It performs a search within module or script installation paths based on the -Name parameter argument. It returns PSResourceInfo objects which describe each resource item found. Other parameters allow the returned results to be filtered by version and path.

## EXAMPLES

### Example 1
```powershell
PS C:\> Get-InstalledPSResource Az
```

This will return versions of the Az module installed via PowerShellGet.

### Example 2
```powershell
PS C:\> Get-InstalledPSResource Az -version "1.0.0"
```

This will return version 1.0.0 of the Az module.

### Example 3
```powershell
PS C:\> Get-InstalledPSResource Az -version "(1.0.0, 3.0.0)"
```

This will return all versions of the Az module within the specified range.

### Example 4
```powershell
PS C:\> Get-InstalledPSResource Az -Path .
```

This will return all versions of the Az module that have been installed in the current directory.

## PARAMETERS

### -Name
Name of a resource or resources to find. Accepts wild card characters.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: True
```

### -Path
Specifies the path to search in.

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

### -Version
Specifies the version of the resource to be returned. Can be an exact version or a version range.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## OUTPUTS

### Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo
```
PSRepositoryItemInfo : {
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

---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# Get-PSResource

## SYNOPSIS
Returns resources (modules and scripts) installed on the machine via PowerShellGet.

## SYNTAX

```
Get-PSResource [[-Name] <String[]>] [-Version <String>] [-Path <String>] [<CommonParameters>]
```

## DESCRIPTION
The Get-PSResource cmdlet combines the Get-InstalledModule, Get-InstalledScript cmdlets from V2. It performs a search within module or script installation paths based on the -Name parameter argument. It returns PSResourceInfo objects which describes each resource item found. Other parameters allow the returned results to be filtered by version and path.

## EXAMPLES

### Example 1
```powershell
PS C:\> Get-PSResource Az
```

This will return versions (stable and prerelease) of the Az module installed via PowerShellGet.

### Example 2
```powershell
PS C:\> Get-PSResource Az -version "1.0.0"
```

This will return version 1.0.0 of the Az module.

### Example 3
```powershell
PS C:\> Get-PSResource Az -version "(1.0.0, 3.0.0)"
```

This will return all versions of the Az module within the specified range.

### Example 4
```powershell
PS C:\> Get-PSResource Az -version "4.0.1-preview"
```

Assume that the package Az version 4.0.1-preview is already installed. This will return version 4.0.1-preview of the Az module.

```powershell
PS C:\> Get-PSResource Az -version "4.0.1"
```
Assume that the package Az version 4.0.1-preview is already installed. This will not return Az version 4.0.1-preview as the full version (including prerelease label, i.e "4.0.1-preview") was not specified.

### Example 5
```powershell
PS C:\> Get-PSResource Az -Version "[4.0.1, 4.0.2-preview]
```

Assume that the following versions are already installed for package Az: 4.0.1-preview and 4.0.2-preview. This will only return version 4.0.2-preview as it is the only one which falls within the specified version range. Per NuGetVersion rules, a prerelease version is less than a stable version, so 4.0.1-preview is less than the 4.0.1 specified version so 4.0.1-preview does not fall within the specified version range and won't be returned.


### Example 6
```powershell
PS C:\> Get-PSResource Az -Path .
```

This will return all versions of the Az module that have been installed in the current directory.

### Example 7
```powershell
PS C:\> Get-PSResource
```

This will return all versions and scripts installed on the machine.

## PARAMETERS

### -Name
Name of a resource or resources to find. Accepts wild card characters or a null value.

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -Path
Specifies the path to search in.

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

### -Version
Specifies the version of the resource to be returned. The value can be an exact version or a version
range using the NuGet versioning syntax.

For more information about NuGet version ranges, see [Package versioning](/nuget/concepts/package-versioning#version-ranges)

PowerShellGet supports all but the _minimum inclusive version_ listed in the NuGet version range
documentation. So inputting "1.0.0.0" as the version doesn't yield versions 1.0.0.0 and higher
(minimum inclusive range). Instead, the values is considered as the required version and yields 
version 1.0.0.0 only (required version). To use the minimum inclusive range, provide `[1.0.0.0, ]` as 
the version range.

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

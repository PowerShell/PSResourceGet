# Get-InstalledPSResource

The `Get-InstalledPSResource` cmdlet combines the `Get-InstalledModule, Get-InstalledScript` cmdlets from V2.
It performs a search within module or script installation paths based on the `-Name` parameter argument.
It returns `PSRepositoryItemInfo` objects which describe each resource item found.
Other parameters allow the returned results to be filtered by version, prerelease version, and path.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-Version <string>] [-Type <string[]>] [-Prerelease] [-Path <string[]>] [-WhatIf] [-Confirm] [<CommonParameters>]
```


## Parameters

### -Name

Name of a resource or resources to find.
Accepts wild card characters.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### -Version

Specifies the version of the resource to be returned.

```yml
Type: string
Parameter Sets: NameParameterSet
```

### -Type

Specifies one or more resource types to find.
Two resource types are supported: Module and Script.

```yml
Type: string[]
Parameter Sets: ResourceNameParameterSet
AllowedValues: 'Module','Script'
```

### -Prerelease

When specified, includes prerelease versions in search.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```

### -Path

Specifies the path to search in. 

```yml
Type: string
Parameter Sets: NameParameterSet
```


### Outputs

```json
"PSRepositoryItemInfo" : {
    "Name",
    "Version",
    "Type",
    "Description",
    "Author",
    "CompanyName",
    "Copyright",
    "PublishedDate",
    "InstalledDate",
    "UpdatedDate",
    "LicenseUri",
    "ProjectUri",
    "IconUri",
    "Tags",
    "Includes",
    "PowerShellGetFormatVersion",
    "ReleaseNotes",
    "Dependencies",
    "RepositorySourceLocation",
    "Repository",
    "PackageManagementProvider",
    "AdditionalMetadata"
}
```

## Notes

Currently the `-Prerelease` parameter is not implemented.  

Add why

## Tests

Most search tests can be performed on a local file system.  

### -Name param

- Single name search
- Wildcard search
- Multiple name search
- Cancel search
- Errors: Not found (single name, wildcard, multiple name)
- Errors: File path: Invalid name, cannot find, etc

### -Version param

- Validate correct resource version returned
- Validate wild card (if supported), correct version range returned
- Errors: Not found
- Errors: Invalid version string format

### -Prerelease param

- Validate prerelease version returned

### -Path param

- Single path search
- OneDrive path search
- Errors: Path not found

## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs  
2 days

### Create test support

Create any test repositories and mocks for tests  
4 days

### Write cmdlet tests

Create all functional tests to validate cmdlet  
5 days


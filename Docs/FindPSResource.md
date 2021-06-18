# Find-PSResource

The `Find-PSResource` cmdlet combines the `Find-Module, Find-Script, Find-DscResource, Find-Command` cmdlets from V2.
It performs a search on a repository (local or remote) based on the `-Name` parameter argument.
It returns `PSResourceInfo` objects which describe each resource item found (with Name, Version, Prerelease and Description information displayed, but other properties available too).
Other parameters allow the returned results to be filtered by item Type, Tag, Version and IncludeDependencies.

Alternatively, a `-CommandName` or `-DscResourceName` can be provided and resource packages having those commands or Dsc resources will be returned. This has not been implemented yet.
The `-ModuleName` parameter allows the command or dsc resource name search to be limited to a subset of module packages. This has not been implemented yet.

## Syntax

### ResourceNameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-Type <Microsoft.PowerShell.PowerShellGet.UtilClasses.ResourceType[]>] [-Version <string>] [-Prerelease] [-Tag <string[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### CommandNameParameterSet
``` PowerShell
[[-CommandName] <string[]>] [-ModuleName <string>] [-Version <string>] [-Prerelease] [-Tag <string[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### DscResourceNameParameterSet
``` PowerShell
[[-DscResourceName] <string[]>] [-ModuleName <string>] [-Version <string>] [-Prerelease] [-Tag <string[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```
### TagParameterSet
``` PowerShell
[[-Name <string>][-Tag <string[]>] [-Prerelease]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### TypeParameterSet
``` PowerShell
[[Name <string>] [-Prerelease]  [-Type <Microsoft.PowerShell.PowerShellGet.UtilClasses.ResourceType[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Name of a resource or resources to find.
Accepts wild card character '*'.

```yml
Type: string[]
Parameter Sets: ResourceNameParameterSet
```

### -Type

Specifies one or more resource types to find.
Resource types supported are: Module, Script, Command, DscResource.

```yml
Type: Microsoft.PowerShell.PowerShellGet.UtilClasses.ResourceType[]
Parameter Sets: ResourceNameParameterSet
AllowedValues: 'Module','Script','DscResource','Command'
```

### -Version

Specifies the version of the resource to be returned.

```yml
Type: string
Parameter Sets: (All)
```

### -Prerelease

When specified, includes prerelease versions in search results returned.

```yml
Type: SwitchParameter
Parameter Sets: (All)
```

### -Tag

Filters search results for resources that include one or more of the specified tags.

```yml
Type: string[]
Parameter Sets: (All)
```

### -Repository

Specifies one or more repository names to search.
If not specified, search will include all currently registered repositories, in order of highest priority, til first repository package is found in.

```yml
Type: string[]
Parameter Sets: (All)
```

### -Credential

Optional credentials to be used when accessing a repository.

```yml
Type: PSCredential
Parameter Sets: (All)
```

### -IncludeDependencies

When specified, search will return all matched resources along with any resources the matched resources depends on.
Dependencies are deduplicated.

```yml
Type: SwitchParameter
Parameter Sets: (All)
```

### -CommandName

Specifies a list of command names that searched module packages will provide.
Wildcards are supported.
Not yet implemented.

```yml
Type: string[]
Parameter Sets: CommandNameParameterSet
```

### -DscResourceName

Specifies a list of dsc resource names that searched module packages will provide.
Wildcards are supported.
Not yet implemented.

```yml
Type: string[]
Parameter Sets: DscResourceNameParameterSet
```

### -ModuleName

Specifies a module resource package name type to search for.
Wildcards are supported.
Not yet implemented.

```yml
Type: string
Parameter Sets: CommandNameParameterSet, DscResourceParameterSet
```

### Outputs

```json
"PSResourceInfo" : {
    "Author",
    "CompanyName",
    "Copyright",
    "Dependencies",
    "Description",
    "IconUri",
    "Includes",
    "InstalledDate",
    "InstalledLocation",
    "IsPrerelease",
    "LicenseUri",
    "Name",
    "PackageManagementProvider",
    "PowerShellGetFormatVersion",
    "PrereleaseLabel",
    "ProjectUri",
    "PublishedDate",
    "ReleaseNotes",
    "Repository",
    "RepositorySourceLocation",
    "Tags",
    "Type",
    "UpdatedDate",
    "Version"
}
```

## Notes

Search strategy will depend on what NuGet APIs support on the server.
PowerShell has its own wildcard behavior that may not be compatible with the NuGet APIs.
The V2 APIs do provide property filtering and some limited wildcard support.
It is not yet clear if V2 or V3 APIs should be used.
Need benefit cost analysis.

Search can be performed on remote or local (file based) repositories, depending on the repository Url.

Does the `-Credential` parameter apply to all repositories being searched?
If so, can that result in an error for repositories that do not require credentials?
How are multiple repository searches that require different credentials handled?
Should searches with credential be limited to a single repository?

Should search results be cached locally on the client machine?
If so, should the cache be per PowerShell session or file based?
At what point should the local cache be updated from the server?
Should there be a parameter switch that forces the search to go directly to the server and skip the local cache?
Should the cache be used if a specific repository is specified?
We should assume that a specific resource package version is identical over any repository from which it is retrieved.

Should a search with no name or 'all' wildcard result in all repository items returned?
Should there be a default limit for number of items returned, and if so can it be bypassed?

The V2 `Find-Command, Find-DscResource` will be combined into `Find-PSResource`.
The `Find-RoleCapability` cmdlet will be dropped for the first release of V3.

## Tests

Most search tests can be performed on a local repository.

Some tests should be performed on remote repository (PSGallery) to verify remote operation, but can be limited.

### -Name param

- Single name search
- Wildcard search
- Multiple name search
- Cancel search
- Errors: Not found (single name, wildcard, multiple name)
- Errors: Repository: Invalid name, connection error, etc

### -Type param

- Validate correct resource type returned

### -Version param

- Validate correct resource version returned
- Validate wild card (if supported), correct version range returned
- Errors: Not found
- Errors: Invalid version string format

### -Prerelease param

- Validate prerelease version returned

### -Tags param

- Validate tag filtering on returned resources

### -Repository param

- All repository search
- Single repository search
- Multiple repository search
- Errors: Repository not found

### -Credential param

- Validate credential search
- Errors: Credential: Invalid

### -IncludeDependencies param

- Validate dependency inclusion in return results

## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs
1 day

### Implement package search helpers

Create helper functions that support all search functions
Use existing code as starting point
4 days

### Investigate V3 API wildcard support

Look into how V3 APIs can be used to reduce what is returned from the server
7 days

### Implement package filtering functions

Create helper functions to provide filtering of search results
3 days

### Investigate and implement local cache

Write mini-design document on local caching strategy
Implement local cache
10 days

### Create test support

Create any test repositories and mocks for tests
4 days

### Write cmdlet tests

Create all functional tests to validate cmdlet
5 days

### Write support function tests

Create all needed tests to validate caching and search helpers
5 days

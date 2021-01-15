# PowerShellGet V3 Module Design

## Description

PowerShellGet V3 is an upgrade to the currently available V2 module.
The V2 module is completely script based, and has dependencies on other PowerShell modules(PackageManagement).  

This version directly uses NuGet APIs, via NuGet managed code binaries.  

For more information, see [Re-architecting PowerShellGet - the PowerShell package manager]().  

## Goals

- Works side by side with current PowerShellGet V2 module

- Remove dependency on PackageManagement module, and directly use NuGet APIs

- Leverage the latest NuGet V3 APIs

- Provide cmdlets that perform similar functions but do not interfere with V2 cmdlets

- Implement as binary cmdlets and minimize use of PowerShell scripts

- Remove unneeded components (DscResources)

- Minimize binary dependencies

- Work over all PowerShell supported platforms

- Minimize code duplication

- Have only one .NET dependency (net461?) for Windows 5.x compatibility

## Compatibility Module

### Update module as needed

### Write/update tests as needed

## Summary of work estimates

### Cmdlet work estimates

TODO:

### Compatibility Module work estimates

TODO:

## Cmdlets

### Find-PSResource

The `Find-PSResource` cmdlet combines the `Find-Module, Find-Script` cmdlets from V2.
It performs a search on a repository (server) based on the `-Name` parameter argument.
It returns `PSRepositoryItemInfo` objects which describe each resource item found.
Other parameters allow the returned results to be filtered by item type and tags.

#### Syntax

``` PowerShell
Find-PSResource [[-Name] <string[]>] [-Type <string[]>] [-Version <string>] [-Prerelease] [-Tags <string[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

#### Parameters

##### -Name

Name of a resource or resources to find.
Accepts wild card characters.

```yml
Type: string[]
```

#### -Type

Specifies one or more resource types to find.
Two resource types are supported: Module and Script.

```yml
Type: string[]
AllowedValues: 'Module','Script'
```

#### -Version

Specifies the version of the resource to be returned.

```yml
Type: string
```

#### -Prerelease

When specified, includes prerelease versions in search.

```yml
Type: SwitchParameter
```

#### -Tags

Filters search results for resources that include one or more of the specified tags.

```yml
Type: string[]
```

#### -Repository

Specifies one or more repository names to search.
If not specified, search will include all currently registered repositories.

```yml
Type: string[]
```

#### -Credential

Optional credentials to be used when accessing a repository.

```yml
Type: PSCredential
```

#### -IncludeDependencies

When specified, search will return all matched resources along with any resources the matched resources depends on.

```yml
Type: SwitchParameter
```

#### Outputs

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

#### Notes

Search strategy will depend on what NuGet APIs support on the server.
I believe V2 APIs do not support wildcard name searches, and that V3 APIs provide limited support.
But PowerShell has its own wildcard behavior that may not be compatible with the NuGet APIs.
PowerShellGet V2 appears to simply retrieve all resources from the server, and then filter as necessary, when a wildcard or filter parameter is used.
This results in very slow operation as PowerShellGallery has more than 6000 resource items.
Search strategy will initially follow V2s behavior.
But some time needs to be spent to see if V3 wildcard support can be used to do more filtering on the server.  

Search can be performed on remote or local (file based) repositories, depending on the repository Url.  

Does `-Version` string parameter handle wild card characters?
If so, how?  

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

#### Tests

Most search tests can be performed on a local repository.  

Some tests should be performed on remote repository (PSGallery) to verify remote operation, but can be limited.  

##### -Name param

- Single name search
- Wildcard search
- Multiple name search
- Cancel search
- Errors: Not found (single name, wildcard, multiple name)
- Errors: Repository: Invalid name, connection error, etc

##### -Type param

- Validate correct resource type returned

##### -Version param

- Validate correct resource version returned
- Validate wild card (if supported), correct version range returned
- Errors: Not found
- Errors: Invalid version string format

##### -Prerelease param

- Validate prerelease version returned

##### -Tags param

- Validate tag filtering on returned resources

##### -Repository param

- All repository search
- Single repository search
- Multiple repository search
- Errors: Repository not found

##### -Credential param

- Validate credential search
- Errors: Credential: Invalid

##### -IncludeDependencies param

- Validate dependency inclusion in return results

#### Work Items

##### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs  
1 day

##### Implement package search helpers

Create helper functions that support all search functions  
Use existing code as starting point  
4 days

##### Investigate V3 API wildcard support

Look into how V3 APIs can be used to reduce what is returned from the server  
7 days

##### Implement package filtering functions

Create helper functions to provide filtering of search results  
3 days

##### Investigate and implement local cache

Write mini-design document on local caching strategy  
Implement local cache  
10 days

##### Create test support

Create any test repositories and mocks for tests  
4 days

##### Write cmdlet tests

Create all functional tests to validate cmdlet  
5 days

##### Write support function tests

Create all needed tests to validate caching and search helpers  
5 days

### Get-PSResource

### Get-PSResourceRepository

### Install-PSResource

### Publish-PSResource

### Register-PSResourceRepository

### Save-PSResource

### Set-PSResourceRepository

### Uninstall-PSResource

### Unregister-PSResourceRepository

### Update-PSResource

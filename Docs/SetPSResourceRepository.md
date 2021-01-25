# Set-PSResourceRepository

The `Set-PSResourceRepository` cmdlet replaces the `Set-PSRepository` cmdlet from V2.

It sets the values for an already registered module repository. Specifically, it sets values for
either the `-URL`, `-Trusted` and `-Priority` parameter arguments by providing the `-Name` parameter argument.

The settings are persistent for the current user and apply to all versions of PowerShell installed for that user.

The `-URL` for the PSGallery repository, which is pre-defined for this repository which is registered by default on each user's PowerShell instance, cannot be set via this cmdlet and will generate an exception.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-Type <string[]>] [-Version <string>] [-Prerelease] [-Tags <string[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RepositoriesParameterSet
``` PowerShell
[[-CommandName] <string[]>] [-ModuleName <string>] [-Version <string>] [-Prerelease] [-Tags <string[]>]
[-Repository <string[]>] [-Credential <pscredential>] [-IncludeDependencies] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Name of the registered repository to be set.
Does not accept wildcard characters.

```yml
Type: string
Parameter Sets: NameParameterSet
```

### -URL

Specifies the location of the repository to be set.
Types of Uri's supported: HTTPS, HTTP, File base, FTP scheme

```yml
Type: Uri
Parameter Sets: NameParameterSet
```

### -Credential

Specifies a user account that has rights to find a resource from a specific repository.

```yml
Type: PSCredential
Parameter Sets: NameParameterSet
```

### -Repositories

Specifies a hashtable of repositories and is used to register multiple repositories at once.

```yml
Type: List<Hashtable>
Parameter Sets: "RepositoriesParameterSet"
```

### -Trusted

Specifies whether the repository should be trusted.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```

### -Proxy

Specifies a proxy server for the request, rather than a direct connection to the internet resource.

```yml
Type: Uri
Parameter Sets: Todo
```

### -ProxyCredential

Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.

```yml
Type: PSCredential
Parameter Sets: (All)
```

### -Priority

Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched before a lower ranking priority one, when searching for a repository item across multiple registered repositories.
Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds to a higher priority ranking than a higher numeric value (i.e 40). Has default value of -1 meaning: todo.
For example, if a item was being searched for across two repositories with the aforementioned ranking than the repository with priority 10 would be searched first.

```yml
Type: int
Parameter Sets: todo
```

### Outputs

```json
"PSRepositoryInfo" : {
    "Name",
    "Url",
    "Trusted",
    "Priority"
}```

## Notes

`-Priority` parameter argument example/explanation here or in Parameter section?
Note about not being able to set PSGallery repo's URL here or in Summary?
What should this type object should this cmdlet return?
Some parameters are missing ParameterSetNames above?

## Tests

Tests added will need to set repositories to URLs with HTTPS, HTTP, file base, and File Transfer Protocol (FTP) URI schemes.

Test to verify Set-PSResourceRepository with PSGallery `-Name` and `-URL` value is not allowed, and generates error with expected error message.

### -Name param

- Single name search
- Errors: Not found (single name)
- Errors: Repository: Invalid name, etc

### -URL param

- Errors: if URL with unsupported type Uri scheme used, or if correct scheme but value is for path/location that doesn't exist

### -Repositories param

- Errors: Invalid Hashtable format

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

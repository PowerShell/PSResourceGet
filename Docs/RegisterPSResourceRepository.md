# Register-PSResourceRepository

The `Register-PSResourceRepository` cmdlet replaces the `Register-PSRepository` from V2.

It registers a repository for PowerShell modules. The repository is registered to the current user's scope
and does not have a system-wide scope.

The Register-PSResourceRepository cmdlet determines which repository will be the default when searching for PowerShell modules. This is done by specifying the priority for a repository when registering it. So later when other cmdlets are used to search for/install a resource, it'll look through all registered repositories (in order of highest ranking priority and then by alphabetical order) until it finds the first match. The aforementioned compatible cmdlets that use this default repository rankings include: Find-PSResource, Install-PSResource, and Publish-PSResource cmdlets.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string>] [-URL <string>] [-Priority <int>] [-Trusted] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### PSGalleryParameterSet
``` PowerShell
[[-Priority <int>] [-Trusted] [-PSGallery] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RepositoriesParameterSet
``` PowerShell
[[-Repositories] <List<Hashtable>>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Name for the repository to be registered.

```yml
Type: string
Parameter Sets: NameParameterSet
```

### -URL

Specifies the location of the repository to be registered.

```yml
Type: Uri
Parameter Sets: NameParameterSet
```

### -PSGallery

When specified, registers PSGallery repository.

```yml
Type: SwitchParameter
Parameter Sets: PSGalleryParameterSet
```

### -Repositories

Specifies a hashtable of repositories and is used to register multiple repositories at once.

```yml
Type: List<Hashtable>
Parameter Sets: RepositoriesParameterSet
```

### -Trusted

Filters search results for resources that include one or more of the specified tags.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet, PSGalleryParameterSet
```

### -Proxy

Specifies a proxy server for the request, rather than a direct connection to the internet resource.

```yml
Type: Uri
Parameter Sets: todo
```

### -ProxyCredential

Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.

```yml
Type: PSCredential
Parameter Sets: todo
```

### -Priority

When specified, search will return all matched resources along with any resources the matched resources depends on.

```yml
Type: int
Parameter Sets: NameParameterSet, PSGalleryParameterSet
```

### -PassThru

When specified, displays the succcessfully registered repository and its information

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet, PSGalleryParameterSet, RepositoriesParameterSet
```

### Outputs

if `-PassThru` not specified output is none

if `-PassThru` is specified output is:

```json
"PSRepositoryInfo" : {
    "Name",
    "Url",
    "Trusted",
    "Priority"
}
```

## Notes

In V2 cmdlets `Register-PSRepository` the `-SourceLocation` parameter (equivalent of `URL` parameter here) dictated uniqueness, so a repository could not be registered if one with the same `-SourceLocation` value had already been registered. However, in V3 uniqueness is dictated by the `-Name` parameter.



## Tests

Tests added will need to register repositories to URLs with HTTPS, HTTP, file base, and File Transfer Protocol (FTP) URI schemes.

### -Name param

- Single name registering
- Errors: Invalid name (i.e with wildcard character)
- Errors: Name is null
- Errors: Name is not unique (another repository is already registered with it)

### -URL param

- Errors: URL is null when NameParameterSet is used

### -Repositories param

- Errors: Expected Hashtable keys not found

### -Priority param

- Validate priority value supplied is in range of 0-50

## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs
1 day

### Implement repository registering function

Write ProcessRecord() function, perhaps split time/task by ParamaterSet and corresponding functionality
1-2 days

### Create test support

Create any test repositories and mocks for tests
1 day

### Write cmdlet tests

Create all functional tests to validate cmdlet
1 day

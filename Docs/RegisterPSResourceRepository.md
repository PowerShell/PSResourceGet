# Register-PSResourceRepository

The `Register-PSResourceRepository` cmdlet replaces the `Register-PSRepository` from V2.

It registers a repository for PowerShell resources. The repository is registered to the user's machine.

The Register-PSResourceRepository cmdlet determines which repository will be the default when searching for PowerShell modules. This is done by specifying the priority for a repository when registering it. So later when other cmdlets are used to search for/install a resource, it will look through all registered repositories (in order of highest ranking priority and then by alphabetical order) until it finds the first match. The aforementioned compatible cmdlets that use this default repository rankings include: Find-PSResource, Install-PSResource, and Publish-PSResource cmdlets.

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

Specifies the URL location of the repository to be registered.

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

Specifies whether the repository should be trusted.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet, PSGalleryParameterSet
```

### -Proxy

Specifies a proxy server for the request, rather than a direct connection to the internet resource.

```yml
Type: Uri
Parameter Sets: NameParameterSet, PSGalleryNamesSet, RepositoriesParameterSet
```

### -ProxyCredential

Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.

```yml
Type: PSCredential
Parameter Sets: NameParameterSet, PSGalleryNamesSet, RepositoriesParameterSet
```

### -Priority

Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched before a lower ranking priority one, when searching for a repository item across multiple registered repositories. Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds to a higher priority ranking than a higher numeric value (i.e 40). Has default value of 50. or example, if a item was being searched for across two repositories with the aforementioned ranking than the repository with priority 10 would be searched first.

```yml
Type: int
Parameter Sets: NameParameterSet, PSGalleryParameterSet
```

### -PassThru

When specified, displays the succcessfully registered repository and its information.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet, PSGalleryParameterSet, RepositoriesParameterSet
```

### Outputs

if `-PassThru` not specified, output is none

if `-PassThru` is specified, output is:

```json
"PSRepositoryInfo" : {
    "Name",
    "Url",
    "Trusted",
    "Priority"
}
```

## Notes

In this V3 cmdlet, repository uniqueness is dictated by the `-Name` parameter. This is unlike in the V2 cmdlet `Register-PSRepository`, whre the `-SourceLocation` parameter (equivalent of `URL` parameter here) dictated uniqueness, so a repository could not be registered if one with the same `-SourceLocation` value had already been registered.

## Tests

Tests able to register repositories with different allowed URL URI schemes.

### -Name param

- Single name registering
- Errors: Invalid name (i.e with wildcard character)
- Errors: Name is null
- Errors: Name is just whitespace
- Errors: Name is not unique (another repository is already registered with same name)
- Errors: Name is "PSGallery" when registering with NameParameterSet

### -URL param

- Errors: URL is null when NameParameterSet is used
- Errors: URL is of invalid URI scheme (i.e not HTTPS, HTTP, FTP, File Based URI scheme)

### -Repositories param

- Errors: Expected Hashtable keys not found
- Errors: PSGallery provided with Name or URI key
- Errors: Name key value is "PSGallery"

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

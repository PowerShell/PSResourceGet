# Set-PSResourceRepository

The `Set-PSResourceRepository` cmdlet replaces the `Set-PSRepository` cmdlet from V2.

It sets the values for an already registered module repository. Specifically, it sets values for
either the `-URL`, `-Trusted` and `-Priority` parameter arguments by additionally providing the `-Name` parameter argument.

The settings are persistent on the machine and apply to all versions of PowerShell installed for that user.

The `-URL` for the PSGallery repository, which is pre-defined for this repository which is registered by default on each user's PowerShell instance, cannot be set via this cmdlet and will generate an exception.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string>] [-URL <string>] [-Credential <PSCredential>] [-Trusted] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RepositoriesParameterSet
``` PowerShell
[[-Repositories] <List[Hashtable]>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Specifies name of the registered repository to be set.
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

Specifies an array of hashtables containing information on repositories and is used to register multiple repositories at once.

```yml
Type: Hashtable[]
Parameter Sets: RepositoriesParameterSet
```

### -Trusted

When specified, repository will be set to trusted.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```

### -Proxy

Specifies a proxy server for the request, rather than a direct connection to the internet resource.

```yml
Type: Uri
Parameter Sets: NameParameterSet, RepositoriesParameterSet
```

### -ProxyCredential

Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.

```yml
Type: PSCredential
Parameter Sets: NameParameterSet, RepositoriesParameterSet
```

### -Priority

Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched before a lower ranking priority one, when searching for a repository item across multiple registered repositories.
Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds to a higher priority ranking than a higher numeric value (i.e 40).
For example, if a item was being searched for across two repositories with the aforementioned ranking than the repository with priority 10 would be searched first.

```yml
Type: int
Parameter Sets: NameParameterSet, RepositoriesParameterSet
```

### -PassThru

When specified, displays the succcessfully set repository and its information

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet, RepositoriesParameterSet
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

## Tests

Tests added will need to set repositories to URLs with different allowed URI schemes.

Test to verify Set-PSResourceRepository with PSGallery `-Name` and `-URL` value is not allowed, and generates error with expected error message.

### -Name param

- Single name search
- Errors: Not found (single name)
- Errors: Invalid name (i.e with wildcard character)
- Errors: Repository: Repository with name not found, etc

### -URL param

- Errors: if URL with unsupported type Uri scheme used, or if correct scheme but value is for path/location that doesn't exist
- Errors: if URL to be changed is used in conjuction with `-Name` parameter argument with value of PSGallery

### -Repositories param

- Errors: Expected Hashtable keys not found

### -Priority param

- Validate priority value supplied is in range of 0-50

## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs
1 day

### Implement repository update function

Create and implement repository update function
1 day

### Create test support

Create any test repositories and mocks for tests
1 day

### Write cmdlet tests

Create all functional tests to validate cmdlet
1 day

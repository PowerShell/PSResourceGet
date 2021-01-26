# Get-PSResourceRepository

The `Get-PSResourceRepository` cmdlet replaces the `Get-PSRepository` cmdlet from V2.

It searches for the PowerShell module repositories that are registered for the current user.
By default it will return all registered repositories, or if the `-Name` parameter argument is specified
then it wil return the repository with that name.
It returns `Object` object.

It returns `PSRepositoryItemInfo` objects which describe each resource item found.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Name of a registered repository to find.
Does not support wild card characters.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### Outputs

```json
"PSRepositoryInfo" : {
    "Name",
    "Url",
    "Trusted",
    "Priority"
}
```

## Notes

Unlike V2's similar cmdlet, this V3 cmdlet does not yet support wildcard characters for the `-Name` parameter
argument.

## Tests

Tests added will need to get repositories registered with HTTPS, HTTP, file base, and File Transfer Protocol (FTP) URI schemes.

Perhaps a test on a fresh machine (with no prior registered repositories) to ensure that PSGallery is registered and able to be retrieved by default?

Do we need to add wildcard search for compatibility with V2 sister cmdlet?

### -Name param

- Single name search
- Multiple name search
- Cancel search
- Errors: Repository not found (single name, multiple name)

## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs
1 day

### Switch output to PSRepositoryInfo type

Switch from returning Object object to PSRepositoryInfo object, and create that class
1-2 days

### Create test support

Create any test repositories and mocks for tests
1 day

### Write cmdlet tests

Create all functional tests to validate cmdlet
1 day
